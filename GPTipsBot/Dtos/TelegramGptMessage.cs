using GPTipsBot.Services;

namespace GPTipsBot.Dtos
{

    public class TelegramGptMessageUpdate
    {
        public TelegramGptMessageUpdate(Telegram.Bot.Types.Message message)
        {
            MessageId = message.MessageId;
            TelegramId = message.From.Id;
            FirstName = message.From.FirstName;
            LastName = message.From.LastName;
            Text = message.Text ?? "";
            ChatId= message.Chat.Id;
            CreatedAt = DateTime.UtcNow;
        }

        public string FirstName { get; set; }
        public string? LastName { get; set; }
        public long TelegramId { get; set; }
        public long ChatId { get; set; }
        public string Text { get; set; }
        public long MessageId { get; set; }
        public string? Reply { get; set; }
        public long? ReplyToId { get; set; }
        public string? Source { get; set; }
        public long? ContextId { get; internal set; }
        public DateTime CreatedAt { get; set; }
        public int ServiceMessageId { get; set; }
    }
}
