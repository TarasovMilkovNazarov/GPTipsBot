using Dapper;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Extensions;
using GPTipsBot.Mapper;
using GPTipsBot.Models;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Data;

namespace GPTipsBot.Repositories
{
    public class MessageRepository : IDisposable
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<TelegramBotWorker> logger;
        
        private readonly string insertMesWithSameContext = "INSERT INTO Messages (text, contextId, userId, chatId, replyToId, createdAt, role, contextBound, telegramMessageId) " +
                "VALUES (@Text, @ContextId, @TelegramId, @ChatId, @ReplyToId, @CreatedAt, @Role, @ContextBound, @TelegramMessageId) RETURNING id, contextId;";
        private readonly string insertWithNewContext = "INSERT INTO Messages (text, userId, chatId, replyToId, createdAt, role, contextBound, telegramMessageId) " +
                "VALUES (@Text, @TelegramId, @ChatId, @ReplyToId, @CreatedAt, @Role, @ContextBound, @TelegramMessageId) RETURNING id, contextId;";

        private readonly string recentContextMessagesQuery = $"SELECT * FROM Messages " +
            $"WHERE userId = @UserId AND chatId = @ChatId AND contextid = @ContextId Order By CreatedAt DESC Limit @Count;";
        private readonly string getLastMessage = $"SELECT * FROM Messages WHERE userid = @TelegramId AND chatid = @ChatId Order By CreatedAt DESC LIMIT 1;";
        private readonly string selectAllUserMessagesQuery = $"SELECT * FROM Messages WHERE UserId = @TelegramId;";

        public MessageRepository(ILogger<TelegramBotWorker> logger, DapperContext context)
        {
            _connection = context.CreateConnection();
            _connection.Open(); 

            this.logger = logger;
        }
        
        public long AddUserMessage(TelegramGptMessageUpdate telegramGptMessage, bool keepContext = true)
        {
            return AddMessage(telegramGptMessage, MessageOwner.User, keepContext);
        }
        
        public long AddBotResponse(TelegramGptMessageUpdate telegramGptMessage)
        {
            return AddMessage(telegramGptMessage, MessageOwner.Assistant);
        }

        public long AddBingImageCreatorResponse(TelegramGptMessageUpdate telegramGptMessage)
        {
            return AddMessage(telegramGptMessage, MessageOwner.BingAI);
        }

        private long AddMessage(TelegramGptMessageUpdate telegramGptMessage, MessageOwner role, bool keepContext = true)
        {
            long? contextId = GetLastContext(telegramGptMessage.UserKey);
            string? text = null;
            long? replyToId = null;

            switch (role)
            {
                case MessageOwner.Assistant:
                    text = telegramGptMessage.Reply;
                    replyToId = telegramGptMessage.MessageId;
                    break;
                case MessageOwner.User:
                    text = telegramGptMessage.Text;
                    break;
                case MessageOwner.BingAI:
                    text = telegramGptMessage.Reply;
                    replyToId = telegramGptMessage.MessageId;
                    break;
                default:
                    break;
            }

            var insertQuery = (keepContext && contextId.HasValue) ? insertMesWithSameContext : insertWithNewContext;

            var inserted = _connection.QuerySingle<(long id, long contextId)>(insertQuery, new { 
                text,
                chatId = telegramGptMessage.UserKey.ChatId,
                contextId,
                telegramId = telegramGptMessage.UserKey.Id,
                replyToId,
                createdAt = DateTime.UtcNow,
                role,
                contextBound = telegramGptMessage.ContextBound,
                telegramMessageId = telegramGptMessage.TelegramMessageId,
            });

            telegramGptMessage.MessageId = inserted.id;
            telegramGptMessage.ContextId = inserted.contextId;

            return telegramGptMessage.MessageId;
        }

        public IEnumerable<Message> GetAllUserMessages(long telegramId)
        {
            IEnumerable<Message> messages = null;

            try
            {
                messages = _connection.Query<Message>(selectAllUserMessagesQuery, new { telegramId });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, null);
            }

            return messages;
        }
        
        public long? GetLastContext(UserKey userKey)
        {
            var lastMes = _connection.QueryFirstOrDefault<Message>(getLastMessage, new {
                telegramId = userKey.Id,
                chatId = userKey.ChatId
            });

            return lastMes?.ContextId;
        }

        public List<Message> GetRecentContextMessages(UserKey userKey, long contextId)
        {
            var messages = _connection.Query<Message>(recentContextMessagesQuery, new {
                userId = userKey.Id,
                chatId = userKey.ChatId,
                contextId,
                count = ContextWindow.WindowSize
            });

            return messages.Where(m => m.ContextBound).ToList();
        }

        public void Dispose()
        {
            _connection.Close(); // Close the connection when the service is disposed
            _connection.Dispose(); // Dispose the connection resources
        }
    }
}
