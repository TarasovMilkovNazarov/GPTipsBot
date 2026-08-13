using System.Text;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Localization;
using GPTipsBot.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Services.Broadcast;

public readonly record struct BroadcastRecipient(long UserId, long TelegramId, string Language);

public sealed class BroadcastService(ApplicationContext context)
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
    };

    public static BroadcastCampaignConfig Deserialize(string json)
    {
        var config = JsonConvert.DeserializeObject<BroadcastCampaignConfig>(json, JsonSettings)
                     ?? new BroadcastCampaignConfig();
        config.ClampDeliveryLimits();
        return config;
    }

    public static string Serialize(BroadcastCampaignConfig config)
    {
        config.ClampDeliveryLimits();
        return JsonConvert.SerializeObject(config, JsonSettings);
    }

    public async Task<BroadcastCampaign> CreateCampaignAsync(
        long adminTelegramId,
        BroadcastCampaignConfig config,
        CancellationToken token)
    {
        config.ClampDeliveryLimits();
        var total = await CountAudienceAsync(config, token);
        var campaign = new BroadcastCampaign
        {
            CreatedByTelegramId = adminTelegramId,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = BroadcastStatus.Draft,
            ConfigJson = Serialize(config),
            TotalTargeted = total,
        };
        context.BroadcastCampaigns.Add(campaign);
        await context.SaveChangesAsync(token);
        return campaign;
    }

    public Task<BroadcastCampaign?> GetAsync(long id, CancellationToken token) =>
        context.BroadcastCampaigns.FirstOrDefaultAsync(c => c.Id == id, token);

    public Task<BroadcastCampaign?> GetRunningAsync(CancellationToken token) =>
        context.BroadcastCampaigns.FirstOrDefaultAsync(c => c.Status == BroadcastStatus.Running, token);

    public async Task<int> CountAudienceAsync(BroadcastCampaignConfig config, CancellationToken token) =>
        await JoinAudience(config).CountAsync(token);

    public async Task<IReadOnlyDictionary<string, int>> CountByLanguageAsync(
        BroadcastCampaignConfig config,
        CancellationToken token)
    {
        config.ClampDeliveryLimits();
        var fallback = config.FallbackLanguage;
        var rows = await JoinAudience(config)
            .GroupBy(r => r.Language)
            .Select(g => new { Language = g.Key, Count = g.Count() })
            .ToListAsync(token);

        return rows
            .GroupBy(r => ResolveAudienceLanguage(r.Language, fallback), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<BroadcastRecipient>> TakeBatchAsync(
        BroadcastCampaignConfig config,
        long afterUserId,
        int take,
        CancellationToken token)
    {
        config.ClampDeliveryLimits();
        var fallback = config.FallbackLanguage;
        var rows = await JoinAudience(config)
            .Where(r => r.UserId > afterUserId)
            .OrderBy(r => r.UserId)
            .Take(take)
            .ToListAsync(token);

        return rows
            .Select(r => new BroadcastRecipient(
                r.UserId,
                r.TelegramId,
                ResolveAudienceLanguage(r.Language, fallback)))
            .ToList();
    }

    public async Task MarkBlockedAsync(IReadOnlyCollection<long> userIds, CancellationToken token)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        await context.Users
            .Where(u => userIds.Contains(u.Id) && u.IsActive)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false), token);
    }

    public static InlineKeyboardMarkup ActionKeyboard() =>
        new(
        [
            [
                InlineKeyboardButton.WithCallbackData("👁 Мне", BroadcastCallbacks.Preview),
                InlineKeyboardButton.WithCallbackData("🧪 Админам", BroadcastCallbacks.Test),
                InlineKeyboardButton.WithCallbackData("🚀 Всем", BroadcastCallbacks.Start),
            ],
            [
                InlineKeyboardButton.WithCallbackData("📊 Статус", BroadcastCallbacks.Status),
                InlineKeyboardButton.WithCallbackData("🛑 Стоп", BroadcastCallbacks.Stop),
                InlineKeyboardButton.WithCallbackData("❌ Сброс", BroadcastCallbacks.Cancel),
            ],
        ]);

    public static string FormatDraftSummary(
        BroadcastCampaignConfig config,
        IReadOnlyDictionary<string, int> byLanguage,
        int total)
    {
        config.ClampDeliveryLimits();
        var sb = new StringBuilder();
        sb.AppendLine("📢 Черновик рассылки");
        sb.AppendLine();
        sb.AppendLine($"Получателей: {total}");
        if (byLanguage.Count > 0)
        {
            sb.AppendLine("По языкам:");
            foreach (var (lang, count) in byLanguage.OrderBy(x => x.Key))
            {
                var hasText = config.Texts.ContainsKey(lang) ? "свой текст" : $"fallback {config.FallbackLanguage}";
                sb.AppendLine($"• {lang}: {count} ({hasText})");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Parse: {config.ParseModeName}");
        sb.AppendLine($"Тихий режим: {(config.DisableNotification ? "да" : "нет")}");
        sb.AppendLine($"Скорость: {config.MessagesPerSecond} сообщ/сек (лимит Telegram ~30)");
        sb.AppendLine($"Только админам: {(config.Audience.AdminsOnly ? "да" : "нет")}");
        if (config.Audience.Languages is { Length: > 0 })
        {
            sb.AppendLine($"Фильтр языков: {string.Join(", ", config.Audience.Languages)}");
        }

        sb.AppendLine();
        sb.AppendLine("Тексты:");
        foreach (var (lang, text) in config.Texts.OrderBy(x => x.Key))
        {
            var preview = text.Length <= 180 ? text : text[..180] + "…";
            sb.AppendLine($"— {lang} ({text.Length} симв.)");
            sb.AppendLine(preview);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatProgress(BroadcastCampaign campaign)
    {
        var processed = campaign.SentCount + campaign.FailedCount + campaign.SkippedCount + campaign.BlockedCount;
        return $"""
                📢 Рассылка #{campaign.Id} — {campaign.Status}
                Отправлено: {campaign.SentCount}
                Заблокировали бота: {campaign.BlockedCount}
                Ошибки: {campaign.FailedCount}
                Пропущено: {campaign.SkippedCount}
                Обработано: {processed} / {campaign.TotalTargeted}
                {(campaign.LastError is null ? "" : "Последняя ошибка: " + campaign.LastError)}
                """.Trim();
    }

    /// <summary>
    /// Scalar projection so EF can translate Count/GroupBy/OrderBy/Take in SQL.
    /// Do not project to <see cref="BroadcastRecipient"/> here — composing on a record struct fails translation.
    /// </summary>
    private IQueryable<AudienceRow> JoinAudience(BroadcastCampaignConfig config)
    {
        config.ClampDeliveryLimits();
        var fallback = config.FallbackLanguage;
        var users = ApplyAudience(context.Users.AsNoTracking(), config);

        var query =
            from user in users
            join settings in context.BotSettings.AsNoTracking() on user.Id equals settings.Id into settingJoin
            from settings in settingJoin.DefaultIfEmpty()
            select new AudienceRow
            {
                UserId = user.Id,
                TelegramId = user.TelegramId ?? 0L,
                Language = settings.Language ?? fallback,
            };

        if (config.Audience.Languages is { Length: > 0 } languages)
        {
            var normalized = languages
                .Select(LocalizationManager.NormalizeLanguage)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            query = query.Where(r => normalized.Contains(r.Language));
        }

        return query;
    }

    private static string ResolveAudienceLanguage(string? language, string fallback) =>
        string.IsNullOrWhiteSpace(language)
            ? fallback
            : LocalizationManager.NormalizeLanguage(language);

    private sealed class AudienceRow
    {
        public long UserId { get; set; }
        public long TelegramId { get; set; }
        public string Language { get; set; } = "";
    }

    private IQueryable<User> ApplyAudience(IQueryable<User> users, BroadcastCampaignConfig config)
    {
        if (config.Audience.ActiveOnly)
        {
            users = users.Where(u => u.IsActive);
        }

        if (config.Audience.TelegramLinkedOnly)
        {
            users = users.Where(u => u.TelegramId != null);
        }

        if (config.Audience.AdminsOnly)
        {
            var adminIds = AppConfig.AdminIds;
            users = users.Where(u => u.TelegramId != null && adminIds.Contains(u.TelegramId.Value));
        }

        if (config.Audience.HasPositiveBalance == true)
        {
            users = users.Where(u => u.Wallet != null && u.Wallet.Balance > 0);
        }

        if (config.Audience.CreatedAfter is DateTimeOffset after)
        {
            users = users.Where(u => u.CreatedAt >= after);
        }

        if (config.Audience.CreatedBefore is DateTimeOffset before)
        {
            users = users.Where(u => u.CreatedAt < before);
        }

        return users;
    }
}
