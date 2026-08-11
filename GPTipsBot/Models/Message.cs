using GPTipsBot.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPTipsBot.Models
{
    public class Message
    {
        public long Id { get; set; }
        public long? TelegramMessageId { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long? ContextId { get; set; }

        [ForeignKey("ReplyTo")]
        public long? ReplyToId { get; set; }
        public Message? ReplyTo { get; set; }
        public string? Text { get; set; }
        public long UserId { get; set; }
        public long ChatId { get; set; }
        /// <summary>Telegram forum topic id; null outside topics / General without thread.</summary>
        public long? MessageThreadId { get; set; }
        public DateTime CreatedAt { get; set; }
        public MessageOwner Role { get; set; }
        public bool ContextBound { get; set; }

        public BotMessageType? Type { get; set; }
    }
}

public enum BotMessageType
{
    Unknown = 0,
    RecognizeText = 1,
    RecognizeVoice = 2,
    ImageGenerated = 3,
    ChatGptPrompt = 4,
    ImagePrompt = 5,
    AnimatedPhoto = 6,
    PromptFromImage = 7,
}