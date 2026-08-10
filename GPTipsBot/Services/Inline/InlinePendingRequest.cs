namespace GPTipsBot.Services.Inline;

public enum InlinePendingKind
{
    Ask,
    Image
}

public sealed class InlinePendingRequest
{
    public required string Id { get; init; }
    public required InlinePendingKind Kind { get; init; }
    public required long TelegramUserId { get; init; }
    public required string Payload { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
