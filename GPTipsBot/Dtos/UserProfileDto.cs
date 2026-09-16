using GPTipsBot.Config;

public class UserProfileDto
{
    public string FirstName { get; set; }
    public string? LastName { get; set; }
    public long Gems { get; set; }
    public long Images { get; set; }
    public long CombinePhotos { get; set; }
    public long ChangePhotos { get; set; }
    public long ImageTexts { get; set; }
    public long GptRequests { get; set; }
    public long PhotoAnimations { get; set; }
    public long Summaries { get; set; }
    public string GptModelDisplayName { get; set; } = GptModelCatalog.Default.DisplayName;
    public string GptModelId { get; set; } = GptModelCatalog.DefaultModelId;

    /// <summary>
    /// Renders the /profile card. Sent with ParseMode.Markdown (legacy) so the balance line can be
    /// bold — see <see cref="EscapeMarkdown"/> for why FirstName/LastName go through it first.
    /// Anyone holding a balance carried it over from the old ⭐ unit, so they get the conversion note
    /// appended; a brand new user with nothing on the balance does not need it. Drop the notice once
    /// the switch is old news.
    /// </summary>
    public string Render()
    {
        var approxMessages = Gems / PaymentConfig.Gpt;
        var balanceBlock = string.Format(
            GPTipsBot.Resources.BotResponse.ProfileBalanceLine, Gems, approxMessages);

        return string.Format(
            GPTipsBot.Resources.BotResponse.ProfileResponse,
            EscapeMarkdown(FirstName), EscapeMarkdown(LastName), balanceBlock, GptRequests, Images,
            ImageTexts, PhotoAnimations, Summaries, GptModelDisplayName, CombinePhotos, ChangePhotos)
        + (Gems > 0
            ? Environment.NewLine + Environment.NewLine + GPTipsBot.Resources.BotResponse.GemsMigrationNotice
            : string.Empty);
    }

    /// <summary>
    /// The card is sent under legacy Markdown so the balance line can be bold. That mode only reserves
    /// four characters (unlike MarkdownV2, which would require escaping the punctuation already used
    /// throughout the static template) — but a first/last name is free text from Telegram and could
    /// still contain one, so it's escaped before going anywhere near the format string.
    /// </summary>
    private static string EscapeMarkdown(string? text) =>
        string.IsNullOrEmpty(text)
            ? string.Empty
            : text
                .Replace("\\", "\\\\")
                .Replace("_", "\\_")
                .Replace("*", "\\*")
                .Replace("`", "\\`")
                .Replace("[", "\\[");
}
