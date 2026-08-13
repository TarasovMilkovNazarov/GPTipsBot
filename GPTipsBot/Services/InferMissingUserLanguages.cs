using GPTipsBot.Db;
using GPTipsBot.Enums;
using GPTipsBot.Localization;
using GPTipsBot.Models;
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

            updated += await ApplyBatchAsync(ids, token);
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
            join settings in context.BotSettings.AsNoTracking() on user.Id equals settings.Id into settingJoin
            from settings in settingJoin.DefaultIfEmpty()
            where user.Id > afterId
                  && (settings == null || settings.Language == null || settings.Language == "")
            orderby user.Id
            select user.Id;

        if (onlyUserIds is { Count: > 0 })
        {
            query = query.Where(id => onlyUserIds.Contains(id));
        }

        return query.Take(take).ToListAsync(token);
    }

    private async Task<int> ApplyBatchAsync(IReadOnlyList<long> ids, CancellationToken token)
    {
        var textsByUser = await LoadRecentUserTextsAsync(ids, token);
        var existing = await context.BotSettings
            .Where(s => ids.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, token);

        var changed = 0;
        foreach (var userId in ids)
        {
            textsByUser.TryGetValue(userId, out var texts);
            var language = MessageLanguageGuess.FromUserTexts(texts ?? []);
            if (existing.TryGetValue(userId, out var settings))
            {
                if (!string.IsNullOrWhiteSpace(settings.Language))
                {
                    continue;
                }

                settings.Language = language;
            }
            else
            {
                context.BotSettings.Add(new BotSettings { Id = userId, Language = language });
            }

            changed++;
        }

        if (changed > 0)
        {
            await context.SaveChangesAsync(token);
            logger.LogInformation("Inferred BotSettings.Language for {Count} users", changed);
        }

        return changed;
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
