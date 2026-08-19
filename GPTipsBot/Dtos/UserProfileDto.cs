using GPTipsBot.Config;

public class UserProfileDto
{
    public string FirstName { get; set; }
    public string? LastName { get; set; }
    public double Stars { get; set; }
    public long Images { get; set; }
    public long CombinePhotos { get; set; }
    public long ChangePhotos { get; set; }
    public long ImageTexts { get; set; }
    public long GptRequests { get; set; }
    public long PhotoAnimations { get; set; }
    public long Summaries { get; set; }
    public string GptModelDisplayName { get; set; } = GptModelCatalog.Default.DisplayName;
    public string GptModelId { get; set; } = GptModelCatalog.DefaultModelId;
}
