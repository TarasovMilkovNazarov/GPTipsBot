using GPTipsBot.Enums;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.Dtos
{
    public class MessageDto
    {
        public long Id { get; set; }
        public long? ContextId { get; set; }
        public string Text { get; set; }
        public long TelegramId { get; set; }
        public long? TelegramMessageId { get; set; }
        public long ChatId { get; set; }
        public DateTime CreatedAt { get; set; }
        public MessageOwner Role { get; set; }
        public long UserId { get; internal set; }
        public IEnumerable<string>? EntityValues { get; internal set; }
        public MessageEntity[]? Entities { get; internal set; }
        public Message? ReplyToMessage { get; internal set; }
        public ChatType ChatType { get; internal set; }
        public MessageType Type { get; internal set; }
        public bool ContextBound { get; internal set; }
        public long ReplyToId { get; internal set; }
        public string? LanguageCode { get; internal set; }
    }
}
