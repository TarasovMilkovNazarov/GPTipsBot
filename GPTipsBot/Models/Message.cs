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
        public Message ReplyTo { get; set; }
        public string Text { get; set; }
        [ForeignKey("User")]
        public long UserId { get; set; }
        public User User { get; set; }
        public long ChatId { get; set; }
        public DateTime CreatedAt { get; set; }
        public MessageOwner Role { get; set; }
        public bool ContextBound { get; set; }
    }
}
