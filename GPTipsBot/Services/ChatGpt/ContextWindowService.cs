using OpenAI.ObjectModels.RequestModels;

namespace GPTipsBot.Services
{
    public class ContextWindow
    {
        public static readonly int WindowSize = 30;
        public static readonly int TokensLimit = 1000;

        private LinkedList<ChatMessage> messages;
        public long TokensCount { get; private set; }

        public ContextWindow()
        {
            messages = new LinkedList<ChatMessage>();
            TokensCount = 0;
        }

        public bool TryToAddMessage(string message, string role, out long messageTokensCount)
        {
            messageTokensCount = 0;

            if (messages.Count > WindowSize)
            {
                return false;
            }

            messageTokensCount = ChatGptService.CountTokens(message);
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
    }
}
