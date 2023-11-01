namespace GPTipsBot.Services
{
    public class MessageService
    {
        public const int MaxMessagesCountPerMinute = 10;
        public static Timer resetMessageCountsPerMinuteTimer;
        public static TimeSpan ResettingInterval => TimeSpan.FromSeconds(60); 

        public static Dictionary<long, int> UserToMessageCount { get; set; }

        public MessageService()
        {
        }

        static MessageService()
        {
            UserToMessageCount = new Dictionary<long, int>();
            resetMessageCountsPerMinuteTimer = new Timer(ResetMessageCountsPerMinute, null, 0, (int)ResettingInterval.TotalMilliseconds);
        }

        public static void ResetMessageCountsPerMinute(Object o)
        {
            foreach (var userId in UserToMessageCount.Keys.ToList())
            {
                UserToMessageCount[userId] = 0;
            }
        }
    }
}
