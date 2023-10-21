using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Models;
using GPTipsBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Repositories
{
    public class MessageRepository
    {
        private readonly ILogger<MessageRepository> logger;
        private readonly ApplicationContext context;

        public MessageRepository(ILogger<MessageRepository> logger, ApplicationContext context)
        {
            this.logger = logger;
            this.context = context;
        }

        public long? AddMessage(MessageDto messageDto, long? replyToId = null)
        {
            long? contextId = messageDto.NewContext ? null : GetLastContext(messageDto.UserId, messageDto.ChatId);

            var newMessage = new Message()
            {
                Text = messageDto.Text,
                ChatId = messageDto.ChatId,
                UserId = messageDto.UserId,
                Role = messageDto.Role,
                ContextId = contextId,
                ContextBound = messageDto.ContextBound,
                TelegramMessageId = messageDto.TelegramMessageId,
                ReplyToId = replyToId,
                CreatedAt = DateTime.UtcNow,
            };

            context.Messages.Add(newMessage);
            context.SaveChanges();

            messageDto.Id = newMessage.Id;
            messageDto.ContextId = newMessage.ContextId;

            return messageDto.ContextId;
        }

        public IEnumerable<Message> GetAllUserMessages(long telegramId)
        {
            return context.Messages.AsNoTracking().Where(x => x.UserId == telegramId);
        }

        public long? GetLastContext(long userId, long chatId)
        {
            var lastMes = context.Messages.AsNoTracking().OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault(x => x.UserId == userId && x.ChatId == chatId && x.ContextId != null);

            return lastMes?.ContextId;
        }

        public List<Message> GetRecentContextMessages(UserChatKey userKey, long contextId)
        {
            var messages = context.Messages.AsNoTracking().Where(x => 
                    x.UserId == userKey.Id && 
                    x.ChatId == userKey.ChatId && 
                    x.ContextId == contextId)
                .Where(m => m.ContextBound)
                .OrderByDescending(x => x.CreatedAt).Take(ContextWindow.WindowSize);

            return messages.ToList();
        }
    }
}
