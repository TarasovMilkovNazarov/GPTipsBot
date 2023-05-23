using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Models
{
    public class Message
    {
        public long Id { get; set; }
        public long? TelegramMessageId { get; set; }
        public long? ContextId { get; set; }
        public long? ReplyToId { get; set; }
        public string Text { get; set; }
        public long UserId { get; set; }
        public long ChatId { get; set; }
        public DateTime CreatedAt { get; set; }
        public GptRolesEnum Role { get; set; }
    }
}
