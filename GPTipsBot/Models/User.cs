using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Models
{
    public class User
    {
        public long Id { get; set; }
        public string FirstName { get; set; }
        public string? LastName { get; set; }
        public long TelegramId { get; set; }
        public string Message { get; set; }
        public DateTimeOffset TimeStamp { get; set; }
        public bool? IsActive { get; set; }
        public long MessagesCount { get; set; }
    }
}
