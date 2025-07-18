using Ardalis.GuardClauses;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Exceptions;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using OpenAI.Chat;
using TiktokenSharp;

namespace GPTipsBot.Services
{
    public class ContextWindow
    {
        public static readonly int WindowSize = 15;
        public static readonly int TokensLimit = 1000;
        private readonly MessageRepository _messageRepository;
        private LinkedList<ChatMessage> _chatMessages;
        public long TokensCount { get; private set; }

        public ContextWindow(MessageRepository messageRepository)
        {
            _chatMessages = new LinkedList<ChatMessage>();
            TokensCount = 0;
            _messageRepository = messageRepository;
        }

        public bool TryToAddMessage(string message, MessageOwner role, out long messageTokensCount)
        {
            messageTokensCount = 0;

            if (_chatMessages.Count > WindowSize)
            {
                return false;
            }

            messageTokensCount = CountTokens(message);
            if (TokensCount + messageTokensCount > TokensLimit)
            {
                return false;
            }

            TokensCount += messageTokensCount;

            ChatMessage chatMessage = null;
            switch (role)
            {
                case MessageOwner.User:
                    chatMessage = ChatMessage.CreateUserMessage(message);
                    break;
                case MessageOwner.Assistant:
                    chatMessage = ChatMessage.CreateAssistantMessage(message);
                    break;
                case MessageOwner.System:
                    chatMessage = ChatMessage.CreateSystemMessage(message);
                    break;
            };

            Guard.Against.Null(chatMessage);

            _chatMessages.AddFirst(chatMessage);

            return true;
        }

        public ChatMessage[] GetContext(UserChatKey userKey, long? contextId)
        {
            if (contextId == null)
            {
                return new ChatMessage[]{};
            }

            var messages = _messageRepository
                .GetRecentContextMessages(userKey, contextId.Value).Where(x => !string.IsNullOrEmpty(x.Text));

            foreach (var item in messages)
            {
                var isMessageAddedToContext = TryToAddMessage(item.Text, item.Role, out var messageTokensCount);
                if (isMessageAddedToContext)
                {
                    continue;
                }

                if (TokensCount == 0)
                {
                    //todo reset context or suggest user to reset: send inline command with reset
                    throw new ClientException(userKey.ChatId, string.Format(BotResponse.TokensLimitExceeded, TokensLimit, messageTokensCount));
                }

                break;
            }

            return _chatMessages.ToArray();
        }

        public static long CountTokens(string message)
        {
            var tikToken = TikToken.EncodingForModel("gpt-3.5-turbo");
            var i = tikToken.Encode(message); //[15339, 1917]

            return i.Count;
        }
    }
}
