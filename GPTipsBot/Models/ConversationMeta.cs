namespace GPTipsBot.Models;

public class ConversationMeta
{
    public long UserId { get; set; }
    public long ContextId { get; set; }
    public string? CustomTitle { get; set; }
    public bool IsPinned { get; set; }
    public bool IsDeleted { get; set; }
}
