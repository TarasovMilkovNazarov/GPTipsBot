using GPTipsBot.Services;
using Telegram.Bot.Types;

namespace GPTipsBot.Dtos
{

    public class TelegramGptMessageUpdate
    {
        public TelegramGptMessageUpdate(Telegram.Bot.Types.Message message)
        {
            MessageId = message.MessageId;
            FirstName = message.From.FirstName;
            LastName = message.From.LastName;
            Text = message.Text ?? "";
            CreatedAt = DateTime.UtcNow;
            IsActive = true;
            ContextBound = true;
            UserKey = new UserKey(message.From.Id, message.Chat.Id);
        }

        public TelegramGptMessageUpdate(CallbackQuery query) : this(query.Message)
        {
            CreatedAt = DateTime.UtcNow;
            IsActive = true;
            ContextBound = false;
            UserKey = new UserKey(query.From.Id, query.Message.Chat.Id);
        }

        public string FirstName { get; set; }
        public string? LastName { get; set; }
        public UserKey UserKey { get; set; }
        public string Text { get; set; }
        public long MessageId { get; set; }
        public string? Reply { get; set; }
        public long? ReplyToId { get; set; }
        public string? Source { get; set; }
        public long? ContextId { get; internal set; }
        public DateTime CreatedAt { get; set; }
        public long ServiceMessageId { get; set; }
        public bool IsActive { get; set; }
        public bool ContextBound { get; set; }
    }
}
