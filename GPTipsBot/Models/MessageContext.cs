using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Models
{
    public class MessageContext : Entity
    {
        public long TelegramId { get; set; }
        public List<Message> Messages { get; set; }
    }
}
