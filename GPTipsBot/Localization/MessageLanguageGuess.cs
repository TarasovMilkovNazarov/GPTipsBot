namespace GPTipsBot.Localization;

public enum MessageLanguageVote
{
    Skip = 0,
    Russian = 1,
    Other = 2,
}

/// <summary>
/// Classifies user texts as Russian vs other by script.
/// Any Cyrillic message counts as Russian so GPT prompts in English do not hide ru users.
/// </summary>
public static class MessageLanguageGuess
{
    public const string Russian = "ru";
    public const string English = "en";

    public static string FromUserTexts(IEnumerable<string?> texts)
    {
        var russian = 0;
        var other = 0;
        foreach (var text in texts)
        {
            switch (ClassifyMessage(text))
            {
                case MessageLanguageVote.Russian:
                    russian++;
                    break;
                case MessageLanguageVote.Other:
                    other++;
                    break;
            }
        }

        // Any Cyrillic wins: GPT users often write prompts in English even when the UI should be ru.
        if (russian > 0)
        {
            return Russian;
        }

        return other > 0 ? English : Russian;
    }

    public static MessageLanguageVote ClassifyMessage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return MessageLanguageVote.Skip;
        }

        var span = text.AsSpan().Trim();
        if (span.StartsWith('/'))
        {
            return MessageLanguageVote.Skip;
        }

        var cyrillic = 0;
        var latin = 0;
        foreach (var c in span)
        {
            if (IsCyrillicLetter(c))
            {
                cyrillic++;
            }
            else if (IsLatinLetter(c))
            {
                latin++;
            }
        }

        if (cyrillic == 0 && latin == 0)
        {
            return MessageLanguageVote.Skip;
        }

        return cyrillic > 0 ? MessageLanguageVote.Russian : MessageLanguageVote.Other;
    }

    private static bool IsCyrillicLetter(char c) =>
        c is (>= '\u0400' and <= '\u04FF')
            or (>= '\u0500' and <= '\u052F')
            or (>= '\u2DE0' and <= '\u2DFF')
            or (>= '\uA640' and <= '\uA69F');

    private static bool IsLatinLetter(char c) =>
        c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');
}
