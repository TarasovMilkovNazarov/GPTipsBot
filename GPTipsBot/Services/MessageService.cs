using GPTipsBot.Repositories;
using OpenAI.GPT3.ObjectModels.RequestModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Services
{
    public class MessageService
    {
        private readonly MessageContextRepository messageContextRepository;
        private readonly ChatGptService chatGptService;
        public const int MaxMessagesCountPerMinute = 5;

        public static Dictionary<long, (int messageCount, DateTime lastMessage)> UserToMessageCount { get; set; }

        public MessageService(MessageContextRepository messageContextRepository, ChatGptService chatGptService)
        {
            this.messageContextRepository = messageContextRepository;
            this.chatGptService = chatGptService;
        }

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

        public ChatMessage[] PrepareContext(long contextId)
        {
            var messages = messageContextRepository.GetRecentContextMessages(contextId);
            var contextWindow = new ContextWindow(new ChatGptService());

            foreach (var item in messages)
            {
                contextWindow.AddMessage(item.Text, item.Role.ToString().ToLower());
            }

            return contextWindow.GetContext();
        }
    }
}
