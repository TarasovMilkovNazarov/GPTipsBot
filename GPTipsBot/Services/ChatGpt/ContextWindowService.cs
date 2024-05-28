using GPTipsBot.Dtos;
using GPTipsBot.Exceptions;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using OpenAI.ObjectModels.RequestModels;
using TiktokenSharp;

namespace GPTipsBot.Services
{
    public class ContextWindow
    {
        public static readonly int WindowSize = 30;
        public static readonly int TokensLimit = 1000;
        private readonly MessageRepository messageRepository;
        private LinkedList<ChatMessage> chatMessages;
        public long TokensCount { get; private set; }

        public ContextWindow(MessageRepository messageRepository)
        {
            chatMessages = new LinkedList<ChatMessage>();
            TokensCount = 0;
            this.messageRepository = messageRepository;
        }

        public bool TryToAddMessage(string message, string role, out long messageTokensCount)
        {
            messageTokensCount = 0;

            if (chatMessages.Count > WindowSize)
            {
                return false;
            }

            messageTokensCount = CountTokens(message);
            if (TokensCount + messageTokensCount > TokensLimit)
            {
                return false;
            }

            TokensCount += messageTokensCount;
            chatMessages.AddFirst(new ChatMessage(role, message));

            return true;
        }

        public ChatMessage[] GetContext(UserChatKey userKey, long contextId)
        {
            var messages = messageRepository
                .GetRecentContextMessages(userKey, contextId).Where(x => !string.IsNullOrEmpty(x.Text));

            foreach (var item in messages)
            {
                var isMessageAddedToContext = TryToAddMessage(item.Text, item.Role.ToString().ToLower(), out var messageTokensCount);
                if (isMessageAddedToContext)
                {
                    continue;
                }

                if (TokensCount == 0)
                {
                    //todo reset context or suggest user to reset: send inline command with reset
                    throw new ClientException(string.Format(BotResponse.TokensLimitExceeded, ContextWindow.TokensLimit, messageTokensCount));
                }

                break;
            }

            return chatMessages.ToArray();
        }

        public static long CountTokens(string message)
        {
            TikToken tikToken = TikToken.EncodingForModel("gpt-3.5-turbo");
            var i = tikToken.Encode(message); //[15339, 1917]

            return i.Count;
        }
    }
}
