using System.Text;
using System.Text.RegularExpressions;
using GPTipsBot.Localization;

namespace GPTipsBot.Services.Broadcast;

public static partial class BroadcastTextParser
{
    [GeneratedRegex(@"^(ru|en|es|fa|ar)\s*:\s*(.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LanguageHeader();

    public static Dictionary<string, string> Parse(string raw, string fallbackLanguage = "ru")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        fallbackLanguage = LocalizationManager.NormalizeLanguage(fallbackLanguage);

        var lines = raw.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var hasHeader = lines.Any(line => LanguageHeader().IsMatch(line.TrimEnd()));
        if (!hasHeader)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [fallbackLanguage] = raw.Trim(),
            };
        }

        var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? currentLang = null;
        var buffer = new StringBuilder();

        foreach (var line in lines)
        {
            var match = LanguageHeader().Match(line);
            if (match.Success)
            {
                Flush(texts, currentLang, buffer);
                currentLang = match.Groups[1].Value.ToLowerInvariant();
                var rest = match.Groups[2].Value;
                if (!string.IsNullOrEmpty(rest))
                {
                    buffer.Append(rest);
                }

                continue;
            }

            currentLang ??= fallbackLanguage;
            if (buffer.Length > 0)
            {
                buffer.Append('\n');
            }

            buffer.Append(line);
        }

        Flush(texts, currentLang, buffer);
        return texts;
    }

    public static string ResolveText(BroadcastCampaignConfig config, string? userLanguage)
    {
        config.ClampDeliveryLimits();
        var lang = LocalizationManager.NormalizeLanguage(userLanguage ?? config.FallbackLanguage);
        if (config.Texts.TryGetValue(lang, out var exact) && !string.IsNullOrWhiteSpace(exact))
        {
            return exact;
        }

        if (config.Texts.TryGetValue(config.FallbackLanguage, out var fallback) &&
            !string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        return config.Texts.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
    }

    public static string? Validate(BroadcastCampaignConfig config)
    {
        config.ClampDeliveryLimits();
        if (config.Texts.Count == 0 || config.Texts.Values.All(string.IsNullOrWhiteSpace))
        {
            return "Нет текста рассылки.";
        }

        foreach (var (lang, text) in config.Texts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (text.Length > BroadcastCampaignConfig.TelegramMaxMessageLength)
            {
                return $"Текст ({lang}) длиннее {BroadcastCampaignConfig.TelegramMaxMessageLength} символов (лимит Telegram).";
            }
        }

        return null;
    }

    private static void Flush(Dictionary<string, string> texts, string? lang, StringBuilder buffer)
    {
        if (lang == null)
        {
            return;
        }

        var text = buffer.ToString().Trim();
        buffer.Clear();
        if (text.Length > 0)
        {
            texts[lang] = text;
        }
    }
}
