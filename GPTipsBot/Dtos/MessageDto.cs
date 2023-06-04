using GPTipsBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Dtos
{
    public class MessageDto
    {
        public long Id { get; set; }
        public long ContextId { get; set; }
        public string Text { get; set; }
        public long TelegramId { get; set; }
        public long? TelegramMessageId { get; set; }
        public long ChatId { get; set; }
        public DateTime CreatedAt { get; set; }
        public MessageOwner Role { get; set; }
    }
}
