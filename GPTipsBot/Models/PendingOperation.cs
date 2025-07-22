using System.ComponentModel.DataAnnotations.Schema;
using GPTipsBot.Dtos;

namespace GPTipsBot.Models;

public class PendingOperation: Entity
{
    public User User { get; set; }
    [ForeignKey("User")]
    public long UserId { get; set; }
    public long ChatId { get; set; }
    public OperationType Type { get; set; }
    public string Data { get; set; }
    public OperationStatus Status { get; set; }

    public PendingOperation(UserChatKey userChatKey, OperationType type)
    {
        Status = OperationStatus.InProgress;
        UserId = userChatKey.Id;
        ChatId = userChatKey.ChatId;
        Type = type;
    }
}

public enum OperationType
{
    Unknown = 0,
    Music,
    Video
}

public enum OperationStatus
{
    Unknown = 0,
    InProgress,
    Completed,
    Failed
}