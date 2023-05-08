using Dapper;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
using GPTipsBot.Models;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Data;

namespace GPTipsBot.Repositories
{
    public class MessageContextRepository : IDisposable
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<TelegramBotWorker> logger;
        
        private readonly string insertMesWithSameContext = "INSERT INTO Messages (text, contextId, userId, chatId, replyToId, createdAt, role) " +
                "VALUES (@Text, @ContextId, @TelegramId, @ChatId, @ReplyToId, @CreatedAt, @Role) RETURNING id, contextId;";
        private readonly string insertWithNewContext = "INSERT INTO Messages (text, userId, chatId, replyToId, createdAt, role) " +
                "VALUES (@Text, @TelegramId, @ChatId, @ReplyToId, @CreatedAt, @Role) RETURNING id, contextId;";
        private readonly string recentContextMessagesQuery = $"SELECT * FROM Messages WHERE contextid = @ContextId Order By CreatedAt DESC Limit @Count;";
        private readonly string getLastMessage = $"SELECT * FROM Messages WHERE userid = @TelegramId AND chatid = @ChatId Order By CreatedAt DESC LIMIT 1;";
        private readonly string getLastContextId = $"SELECT contextid FROM Messages Order By ContextId DESC LIMIT 1;";
        private readonly string selectAllUserMessagesQuery = $"SELECT * FROM Messages WHERE UserId = @TelegramId;";

        public MessageContextRepository(ILogger<TelegramBotWorker> logger, DapperContext context)
        {
            _connection = context.CreateConnection();
            _connection.Open(); 

            this.logger = logger;
        }
        
        public long AddUserMessage(TelegramGptMessage telegramGptMessage)
        {
            return AddMessage(telegramGptMessage, GptRolesEnum.User);
        }

        public long AddBotResponse(TelegramGptMessage telegramGptMessage)
        {
            return AddMessage(telegramGptMessage, GptRolesEnum.Assistant);
        }

        private long AddMessage(TelegramGptMessage telegramGptMessage, GptRolesEnum role)
        {
            long? contextId = GetLastContext(telegramGptMessage.TelegramId, telegramGptMessage.ChatId);
            string? text = null;
            long? replyToId = null;

            switch (role)
            {
                case GptRolesEnum.Assistant:
                    text = telegramGptMessage.Reply;
                    replyToId = telegramGptMessage.MessageId;
                    break;
                case GptRolesEnum.User:
                    text = telegramGptMessage.Message;
                    break;
                default:
                    break;
            }

            var insertQuery = contextId.HasValue ? insertMesWithSameContext : insertWithNewContext;

            var inserted = _connection.QuerySingle<(long id, long contextId)>(insertQuery, new { 
                text,
                chatId = telegramGptMessage.ChatId,
                contextId,
                telegramId = telegramGptMessage.TelegramId,
                replyToId,
                createdAt = DateTime.UtcNow,
                role
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
                logger.LogWithStackTrace(LogLevel.Error, $"GetAllMessages for {telegramId}. Error {ex.Message}");
            }

            return messages;
        }
        
        public long? GetLastContext(long telegramId, long chatId)
        {
            var lastMes = _connection.QueryFirstOrDefault<Message>(getLastMessage, new {
                telegramId,
                chatId
            });

            return lastMes?.ContextId;
        }

        public List<Message> GetRecentContextMessages(long contextId)
        {
            var messages = _connection.Query<Message>(recentContextMessagesQuery, new {
                contextId,
                count = ContextWindow.WindowSize
            });

            return messages.ToList();
        }

        public void ResetContext(TelegramGptMessage telegramGptMessage, long chatId)
        {
            telegramGptMessage.MessageId = _connection.QuerySingle<long>(insertMesWithSameContext, new { 
                text = "/reset",
                contextId = 0,
                chatId,
                telegramId = telegramGptMessage.TelegramId,
                createdAt = DateTime.UtcNow
            });
        }

        public void Dispose()
        {
            _connection.Close(); // Close the connection when the service is disposed
            _connection.Dispose(); // Dispose the connection resources
        }
    }
}
