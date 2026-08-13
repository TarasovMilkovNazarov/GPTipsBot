using Telegram.Bot.Types.Enums;

namespace GPTipsBot.Services.Broadcast;

/// <summary>
/// Extensible campaign payload stored as JSON. Add fields here as mailing grows;
/// unknown extra JSON is ignored on deserialize and new fields get defaults.
/// </summary>
public sealed class BroadcastCampaignConfig
{
    public const int TelegramMaxMessageLength = 4096;
    /// <summary>Bot API global cap is ~30 msg/s; stay below to reduce 429s.</summary>
    public const int TelegramMaxMessagesPerSecond = 30;
    public const int DefaultMessagesPerSecond = 25;
    public const int DefaultBatchSize = 100;
    public const int DefaultProgressEvery = 100;

    public Dictionary<string, string> Texts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Used when the user has no matching per-language text.</summary>
    public string FallbackLanguage { get; set; } = "ru";

    /// <summary>plain | markdown | markdownv2 | html</summary>
    public string ParseModeName { get; set; } = "plain";

    public bool DisableWebPagePreview { get; set; } = true;
    public bool DisableNotification { get; set; }

    /// <summary>1..30. Telegram floods the bot if this is higher.</summary>
    public int MessagesPerSecond { get; set; } = DefaultMessagesPerSecond;

    public int BatchSize { get; set; } = DefaultBatchSize;
    public int ProgressEvery { get; set; } = DefaultProgressEvery;

    public BroadcastAudience Audience { get; set; } = new();

    // --- reserved for later (serialized, ignored until wired) ---

    /// <summary>Send as photo caption instead of a text message.</summary>
    public string? PhotoFileId { get; set; }

    public List<BroadcastUrlButton>? Buttons { get; set; }

    public DateTimeOffset? ScheduledAt { get; set; }

    public ParseMode? ToTelegramParseMode() => (ParseModeName ?? "plain").Trim().ToLowerInvariant() switch
    {
        "markdown" or "md" => ParseMode.Markdown,
        "markdownv2" or "mdv2" => ParseMode.MarkdownV2,
        "html" => ParseMode.Html,
        _ => null,
    };

    public void ClampDeliveryLimits()
    {
        MessagesPerSecond = Math.Clamp(MessagesPerSecond, 1, TelegramMaxMessagesPerSecond);
        BatchSize = Math.Clamp(BatchSize, 1, 500);
        ProgressEvery = Math.Clamp(ProgressEvery, 10, 1000);
        FallbackLanguage = string.IsNullOrWhiteSpace(FallbackLanguage) ? "ru" : FallbackLanguage.ToLowerInvariant();
        ParseModeName = string.IsNullOrWhiteSpace(ParseModeName) ? "plain" : ParseModeName;
        Audience ??= new BroadcastAudience();
        var source = Texts ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in source)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                Texts[key.Trim().ToLowerInvariant()] = value;
            }
        }
    }
}

public sealed class BroadcastAudience
{
    public bool ActiveOnly { get; set; } = true;
    public bool TelegramLinkedOnly { get; set; } = true;

    /// <summary>Send only to AppConfig.AdminIds (dry-run / smoke test).</summary>
    public bool AdminsOnly { get; set; }

    /// <summary>Null or empty = every language. Values are ru/en/es/fa/ar.</summary>
    public string[]? Languages { get; set; }

    // --- reserved for later ---

    public bool? HasPositiveBalance { get; set; }
    public DateTimeOffset? CreatedAfter { get; set; }
    public DateTimeOffset? CreatedBefore { get; set; }
}

public sealed class BroadcastUrlButton
{
    public string Text { get; set; } = "";
    public string Url { get; set; } = "";
}
