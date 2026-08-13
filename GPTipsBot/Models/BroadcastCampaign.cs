namespace GPTipsBot.Models;

public enum BroadcastStatus
{
    Draft = 0,
    Running = 1,
    Completed = 2,
    Cancelled = 3,
    Failed = 4,
}

public class BroadcastCampaign
{
    public long Id { get; set; }
    public long CreatedByTelegramId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public BroadcastStatus Status { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public long LastUserId { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public int BlockedCount { get; set; }
    public int TotalTargeted { get; set; }
    public string? LastError { get; set; }
}
