using Dapper;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
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
                "VALUES (@Text, @ContextId, @UserId, @ChatId, @ReplyToId, @CreatedAt, @Role, @ContextBound, @TelegramMessageId) RETURNING id, contextId;";
        private readonly string insertWithNewContext = "INSERT INTO Messages (text, userId, chatId, replyToId, createdAt, role, contextBound, telegramMessageId) " +
                "VALUES (@Text, @UserId, @ChatId, @ReplyToId, @CreatedAt, @Role, @ContextBound, @TelegramMessageId) RETURNING id, contextId;";

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

        public long AddMessage(MessageDto messageDto, long? replyToId = null, bool keepContext = true)
        {
            long? contextId = GetLastContext(messageDto.UserId, messageDto.ChatId);

            var insertQuery = (keepContext && contextId.HasValue) ? insertMesWithSameContext : insertWithNewContext;

            var inserted = _connection.QuerySingle<(long id, long contextId)>(insertQuery, new { 
                messageDto.Text,
                messageDto.ChatId,
                contextId,
                messageDto.UserId,
                replyToId,
                createdAt = DateTime.UtcNow,
                messageDto.Role,
                messageDto.ContextBound,
                messageDto.TelegramMessageId,
            });

            messageDto.Id = inserted.id;
            messageDto.ContextId = inserted.contextId;

            return inserted.id;
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
        
        public long? GetLastContext(long userId, long chatId)
        {
            var lastMes = _connection.QueryFirstOrDefault<Message>(getLastMessage, new {
                telegramId = userId,
                chatId
            });

            return lastMes?.ContextId;
        }

        public List<Message> GetRecentContextMessages(UserChatKey userKey, long contextId)
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
