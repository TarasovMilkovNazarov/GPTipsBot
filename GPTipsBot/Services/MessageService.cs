using GPTipsBot.Dtos;
using GPTipsBot.Repositories;
using OpenAI.ObjectModels.RequestModels;

namespace GPTipsBot.Services
{
    public class MessageService
    {
        private readonly MessageRepository messageContextRepository;
        private readonly ChatGptService chatGptService;
        public const int MaxMessagesCountPerMinute = 5;
        public static Timer resetMessageCountsPerMinuteTimer;
        public static TimeSpan ResettingInterval => TimeSpan.FromSeconds(60); 

        public static Dictionary<long, int> UserToMessageCount { get; set; }

        public MessageService(MessageRepository messageContextRepository, ChatGptService chatGptService)
        {
            this.messageContextRepository = messageContextRepository;
            this.chatGptService = chatGptService;
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

        public ChatMessage[] PrepareContext(UserKey userKey, long contextId)
        {
            var messages = messageContextRepository.GetRecentContextMessages(userKey, contextId);
            var contextWindow = new ContextWindow(new ChatGptService());

            foreach (var item in messages)
            {
                var isMessageAddedToContext = contextWindow.TryToAddMessage(item.Text, item.Role.ToString().ToLower());
                if (!isMessageAddedToContext) break;
            }

            return contextWindow.GetContext();
        }
    }
}
