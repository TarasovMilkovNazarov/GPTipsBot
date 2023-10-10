using OpenAI.ObjectModels.RequestModels;

namespace GPTipsBot.Services
{
    public class ContextWindow
    {
        private LinkedList<ChatMessage> messages;
        public static readonly int WindowSize = 30;
        private readonly ChatGptService chatGptService;
        private long tokensCount;

        public ContextWindow(ChatGptService chatGptService)
        {
            messages = new LinkedList<ChatMessage>();
            this.chatGptService = chatGptService;
            tokensCount = 0;
        }

        public bool TryToAddMessage(string message, string role)
        {
            if (messages.Count < WindowSize)
            {
                var messageTokensCount = chatGptService.CountTokens(message);
                if (messageTokensCount + tokensCount <= ChatGptService.MaxTokensLimit)
                {
                    tokensCount += messageTokensCount;
                    messages.AddFirst(new ChatMessage(role, message));

                    return true;
                }
            }

            return false;
        }

        public ChatMessage[] GetContext()
        {
            return messages.ToArray();
        }

        public static string TryToPruneMessage(string message)
        {
            throw new NotImplementedException();

            return message;
        }
    }
}
