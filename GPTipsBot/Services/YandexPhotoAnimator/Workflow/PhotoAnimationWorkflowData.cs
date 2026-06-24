namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow;

public class PhotoAnimationWorkflowData
{
    public long ChatId { get; set; }
    public long UserId { get; set; }
    public string ImageFileId { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public int? ProgressMessageId { get; set; }
    public string? Base64Image { get; set; }
    public string? ImageUrl { get; set; }
    public string? GenerationId { get; set; }
    public string? VideoUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public int PollAttempt { get; set; }
}
