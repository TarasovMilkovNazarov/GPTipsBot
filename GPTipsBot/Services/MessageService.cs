using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Services
{
    public class MessageService
    {
        public const int MaxMessagesCountPerMinute = 2;
        public static Dictionary<long, (int messageCount, DateTime lastMessage)> UserToMessageCount { get; set; }

        static MessageService()
        {
            UserToMessageCount = new Dictionary<long, (int messageCount, DateTime lastMessage)>();
            Timer timer = new Timer(ResetMessageCountTime, null, 0, 60 * 1000 * 1);
        }

        public static void ResetMessageCountTime(Object o)
        {
            foreach (var userId in UserToMessageCount.Keys.ToList())
            {
                UserToMessageCount[userId] = (0, DateTime.MinValue);
            }
        }

    }
}
