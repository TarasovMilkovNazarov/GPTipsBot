using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Models
{
    public class Message
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public long UserId { get; set; }
    }
}
