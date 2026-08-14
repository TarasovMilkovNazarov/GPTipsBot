using GPTipsBot.Db;
using GPTipsBot.Enums;
using GPTipsBot.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Services;

public sealed class InferMissingUserLanguages(ApplicationContext context, ILogger<InferMissingUserLanguages> logger)
{
    public const int UserBatchSize = 50;
    public const int MessagesPerUser = 80;

    public Task<int> RunAsync(CancellationToken token) => RunAsync(null, token);

    public async Task<int> RunAsync(IReadOnlyCollection<long>? onlyUserIds, CancellationToken token)
    {
        var updated = 0;
        var afterId = 0L;
        while (!token.IsCancellationRequested)
        {
            var ids = await MissingUserIdsAsync(afterId, UserBatchSize, onlyUserIds, token);
            if (ids.Count == 0)
            {
                break;
            }

            try
            {
                updated += await ApplyBatchAsync(ids, token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(
                    ex,
                    "Language backfill batch failed for users {FirstId}-{LastId}; continuing",
                    ids[0],
                    ids[^1]);
            }

            afterId = ids[^1];
        }

        return updated;
    }

    private Task<List<long>> MissingUserIdsAsync(
        long afterId,
        int take,
        IReadOnlyCollection<long>? onlyUserIds,
        CancellationToken token)
    {
        var query =
            from user in context.Users.AsNoTracking()
            where user.Id > afterId
                  && !context.BotSettings.Any(s =>
                      s.Id == user.Id
                      && s.Language != null
                      && s.Language != ""
                      && !s.Language.ToLower().StartsWith("en"))
            select user.Id;

        if (onlyUserIds is { Count: > 0 })
        {
            query = query.Where(id => onlyUserIds.Contains(id));
        }

        return query.OrderBy(id => id).Take(take).ToListAsync(token);
    }

    private async Task<int> ApplyBatchAsync(IReadOnlyList<long> ids, CancellationToken token)
    {
        var uniqueIds = ids.Distinct().ToList();
        var textsByUser = await LoadRecentUserTextsAsync(uniqueIds, token);
        var existingLanguages = await LoadLanguagesByUserIdAsync(uniqueIds, token);

        var toWrite = new List<(long Id, string Language)>();
        foreach (var userId in uniqueIds)
        {
            textsByUser.TryGetValue(userId, out var texts);
            var language = MessageLanguageGuess.FromUserTexts(texts ?? []);
            if (existingLanguages.TryGetValue(userId, out var current)
                && !ShouldReplaceLanguage(current, language))
            {
                continue;
            }

            toWrite.Add((userId, language));
        }

        if (toWrite.Count == 0)
        {
            return 0;
        }

        var changed = 0;
        foreach (var (userId, language) in toWrite)
        {
            try
            {
                var affected = await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "BotSettings" ("Id", "Language")
                    VALUES ({userId}, {language})
                    ON CONFLICT ("Id") DO UPDATE
                    SET "Language" = EXCLUDED."Language"
                    WHERE COALESCE(btrim("BotSettings"."Language"), '') = ''
                       OR lower("BotSettings"."Language") LIKE 'en%'
                    """, token);
                if (affected > 0)
                {
                    changed++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to upsert BotSettings.Language for user {UserId}", userId);
            }
        }

        context.ChangeTracker.Clear();
        logger.LogInformation("Inferred BotSettings.Language for {Count} users", changed);
        return changed;
    }

    private static bool ShouldReplaceLanguage(string? current, string inferred)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return true;
        }

        if (string.Equals(current, inferred, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return LocalizationManager.NormalizeLanguage(current) == MessageLanguageGuess.English;
    }

    private async Task<Dictionary<long, string?>> LoadLanguagesByUserIdAsync(
        IReadOnlyList<long> ids,
        CancellationToken token)
    {
        var rows = await context.BotSettings.AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.Language })
            .ToListAsync(token);

        return rows
            .GroupBy(r => r.Id)
            .ToDictionary(g => g.Key, g => g.First().Language);
    }

    private async Task<Dictionary<long, List<string>>> LoadRecentUserTextsAsync(
        IReadOnlyList<long> ids,
        CancellationToken token)
    {
        var idArray = ids.ToArray();
        var role = (int)MessageOwner.User;
        var take = MessagesPerUser;
        var rows = await context.Database
            .SqlQuery<UserMessageTextRow>($"""
                SELECT "UserId", "Text"
                FROM (
                    SELECT m."UserId", m."Text",
                           ROW_NUMBER() OVER (PARTITION BY m."UserId" ORDER BY m."CreatedAt" DESC) AS rn
                    FROM "Messages" m
                    WHERE m."UserId" = ANY({idArray})
                      AND m."Role" = {role}
                      AND m."Text" IS NOT NULL
                      AND btrim(m."Text") <> ''
                ) q
                WHERE q.rn <= {take}
                """)
            .ToListAsync(token);

        return rows
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Text).ToList());
    }

    private sealed class UserMessageTextRow
    {
        public long UserId { get; set; }
        public string Text { get; set; } = "";
    }
}
