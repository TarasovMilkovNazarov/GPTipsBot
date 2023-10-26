using OpenAI.ObjectModels.RequestModels;

namespace GPTipsBot.Services
{
    public class ContextWindow
    {
        public static readonly int WindowSize = 30;
        public static readonly int TokensLimit = 1000;

        private readonly ChatGptService chatGptService;
        private LinkedList<ChatMessage> messages;
        public long TokensCount { get; private set; }

        public ContextWindow(ChatGptService chatGptService)
        {
            messages = new LinkedList<ChatMessage>();
            this.chatGptService = chatGptService;
            TokensCount = 0;
        }

        public bool TryToAddMessage(string message, string role, out long messageTokensCount)
        {
            messageTokensCount = 0;

            if (messages.Count > WindowSize)
            {
                return false;
            }

            messageTokensCount = chatGptService.CountTokens(message);
            if (TokensCount + messageTokensCount > TokensLimit)
            {
                return false;
            }

            TokensCount += messageTokensCount;
            messages.AddFirst(new ChatMessage(role, message));

            return true;
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
