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
        private readonly ILogger<MessageRepository> _logger;
        private readonly ApplicationContext _context;

        public MessageRepository(ILogger<MessageRepository> logger, ApplicationContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<Message> AddAsync(MessageDto messageDto, Message? replyTo = null)
        {
            var contextId = messageDto is { ContextBound: true, NewContext: false } ?
                GetLastContext(messageDto.UserId, messageDto.ChatId) : null;

            var newMessage = new Message()
            {
                Text = messageDto.Text,
                ChatId = messageDto.ChatId,
                UserId = messageDto.UserId,
                Role = messageDto.Role,
                ContextId = contextId,
                ContextBound = messageDto.ContextBound,
                TelegramMessageId = messageDto.TelegramMessageId,
                ReplyTo = replyTo,
                CreatedAt = DateTime.UtcNow,
                Type = messageDto.BotMessageType
            };

            _context.Messages.Add(newMessage);

            await _context.SaveChangesAsync();

            messageDto.Id = newMessage.Id;
            messageDto.ContextId = newMessage.ContextId;

            return newMessage;
        }

        public IEnumerable<Message> GetAllUserMessages(long telegramId)
        {
            return _context.Messages.AsNoTracking().Where(x => x.UserId == telegramId);
        }

        public long? GetLastContext(long userId, long chatId)
        {
            var lastMes = _context.Messages.AsNoTracking().OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault(x => x.UserId == userId && x.ChatId == chatId && x.ContextId != null);

            return lastMes?.ContextId;
        }
        
        public List<Message> GetRecentContextMessages(UserChatKey userKey, long contextId)
        {
            var messages = _context.Messages.AsNoTracking().Where(x =>
                    x.UserId == userKey.Id && 
                    x.ChatId == userKey.ChatId && 
                    x.ContextId == contextId)
                .Where(m => m.ContextBound)
                .OrderByDescending(x => x.CreatedAt).Take(ContextWindow.WindowSize);

            return messages.ToList();
        }

        public int GetTodayImagesCount(UserChatKey userKey)
        {
            var imagesCount = _context.Messages.AsNoTracking()
                .Where(x => x.UserId == userKey.Id && x.Type == BotMessageType.ImageGenerated)
                .Count(m => m.CreatedAt.Date == DateTime.UtcNow.Date)
                ;

            return imagesCount;
        }

        public int GetTodayTextRecognitionCount(UserChatKey userKey)
        {
            var imagesCount = _context.Messages.AsNoTracking()
                .Where(x => x.Type == BotMessageType.RecognizeText)
                .Count(m => m.CreatedAt.Date == DateTime.UtcNow.Date);

            return imagesCount;
        }
    }
}
