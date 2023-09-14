using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Models;
using GPTipsBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;

namespace GPTipsBot.Repositories
{
    public class MessageRepository
    {
        private readonly ILogger<TelegramBotWorker> logger;
        private readonly ApplicationContext context;

        public MessageRepository(ILogger<TelegramBotWorker> logger, ApplicationContext context)
        {
            this.logger = logger;
            this.context = context;
        }

        public long AddMessage(MessageDto messageDto, long? replyToId = null)
        {
            long? contextId = GetLastContext(messageDto.UserId, messageDto.ChatId);

            var newMessage = new Message()
            {
                Text = messageDto.Text,
                ChatId = messageDto.ChatId,
                UserId = messageDto.UserId,
                Role = messageDto.Role,
                ContextBound = messageDto.ContextBound,
                TelegramMessageId = messageDto.TelegramMessageId,
                ReplyToId = replyToId,
                ContextId = contextId,
                CreatedAt = DateTime.UtcNow,
            };

            var added = context.Messages.Add(newMessage);
            context.SaveChanges();

            messageDto.Id = added.Entity.Id;
            messageDto.ContextId = added.Entity.ContextId;

            return messageDto.ContextId.Value;
        }

        public IEnumerable<Message> GetAllUserMessages(long telegramId)
        {
            return context.Messages.AsNoTracking().Where(x => x.UserId == telegramId);
        }

        public long? GetLastContext(long userId, long chatId)
        {
            var lastMes = context.Messages.AsNoTracking().OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault(x => x.UserId == userId && x.ChatId == chatId);

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
