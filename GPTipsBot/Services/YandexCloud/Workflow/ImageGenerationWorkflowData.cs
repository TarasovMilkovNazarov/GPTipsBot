namespace GPTipsBot.Services.YandexCloud.Workflow;

public class ImageGenerationWorkflowData
{
    public long ChatId { get; set; }
    public long ReplyChatId { get; set; }
    public long UserId { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public bool IsSquare { get; set; }
    public int? ProgressMessageId { get; set; }
    /// <summary>When set, deliver the result by editing an inline message instead of SendPhoto.</summary>
    public string? InlineMessageId { get; set; }
    public string? OperationId { get; set; }
    public string? ImageBase64 { get; set; }
    public bool UseDevPlaceholder { get; set; }
    public DateTime? DeadlineUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public long? PaymentHoldId { get; set; }

    public long DeliveryChatId => ReplyChatId != 0 ? ReplyChatId : ChatId;
    public bool IsInlineDelivery => !string.IsNullOrEmpty(InlineMessageId);
}
