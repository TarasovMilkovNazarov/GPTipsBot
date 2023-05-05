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
        public long ContextId { get; set; }
        public string Text { get; set; }
        public long UserId { get; set; }
        public long TelegramId { get; set; }
        public long ChatId { get; set; }
        public DateTime TimeStamp { get; set; }
        public GptRolesEnum Roles { get; set; }
    }
}
