namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow;

public enum AliceImageKind
{
    Editing = 0,
    Combining = 1,
}

public class AliceImageWorkflowData
{
    public AliceImageKind Kind { get; set; }
    public long ChatId { get; set; }
    public long UserId { get; set; }
    public string ImageFileId { get; set; } = string.Empty;
    public string? ImageFileId2 { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public int? ProgressMessageId { get; set; }
    public string? Base64Image { get; set; }
    public string? Base64Image2 { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageUrl2 { get; set; }
    public string? GenerationId { get; set; }
    public string? ResultImageUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public long? PaymentHoldId { get; set; }
    public string? UiLanguage { get; set; }
}
