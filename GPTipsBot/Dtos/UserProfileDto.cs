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
    /// Renders the /profile card. Anyone holding a balance carried it over from the old ⭐ unit, so
    /// they get the conversion note appended; a brand new user with nothing on the balance does not
    /// need it. Drop the notice once the switch is old news.
    /// </summary>
    public string Render() =>
        string.Format(
            GPTipsBot.Resources.BotResponse.ProfileResponse,
            FirstName, LastName, Gems, GptRequests, Images, ImageTexts,
            PhotoAnimations, Summaries, GptModelDisplayName, CombinePhotos, ChangePhotos)
        + (Gems > 0
            ? Environment.NewLine + Environment.NewLine + GPTipsBot.Resources.BotResponse.GemsMigrationNotice
            : string.Empty);
}
