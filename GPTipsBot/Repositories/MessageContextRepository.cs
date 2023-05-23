using Dapper;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
using GPTipsBot.Mapper;
using GPTipsBot.Models;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Data;
using static System.Net.Mime.MediaTypeNames;

namespace GPTipsBot.Repositories
{
    public class MessageContextRepository : IDisposable
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<TelegramBotWorker> logger;
        
        private readonly string insertMesWithSameContext = "INSERT INTO Messages (text, contextId, userId, chatId, replyToId, createdAt, role, contextBound) " +
                "VALUES (@Text, @ContextId, @TelegramId, @ChatId, @ReplyToId, @CreatedAt, @Role, @ContextBound) RETURNING id, contextId;";
        private readonly string insertWithNewContext = "INSERT INTO Messages (text, userId, chatId, replyToId, createdAt, role, contextBound) " +
                "VALUES (@Text, @TelegramId, @ChatId, @ReplyToId, @CreatedAt, @Role, @ContextBound) RETURNING id, contextId;";

        private readonly string recentContextMessagesQuery = $"SELECT * FROM Messages " +
            $"WHERE userId = @UserId AND chatId = @ChatId AND contextid = @ContextId Order By CreatedAt DESC Limit @Count;";
        private readonly string getLastMessage = $"SELECT * FROM Messages WHERE userid = @TelegramId AND chatid = @ChatId Order By CreatedAt DESC LIMIT 1;";
        private readonly string selectAllUserMessagesQuery = $"SELECT * FROM Messages WHERE UserId = @TelegramId;";

        public MessageContextRepository(ILogger<TelegramBotWorker> logger, DapperContext context)
        {
            _connection = context.CreateConnection();
            _connection.Open(); 

            this.logger = logger;
        }
        
        public long AddUserMessage(TelegramGptMessageUpdate telegramGptMessage, bool keepContext = true)
        {
            return AddMessage(telegramGptMessage, GptRolesEnum.User, keepContext);
        }

        public long AddBotResponse(TelegramGptMessageUpdate telegramGptMessage)
        {
            return AddMessage(telegramGptMessage, GptRolesEnum.Assistant);
        }

        private long AddMessage(TelegramGptMessageUpdate telegramGptMessage, GptRolesEnum role, bool keepContext = true)
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
                    text = telegramGptMessage.Text;
                    break;
                default:
                    break;
            }

            var insertQuery = (keepContext && contextId.HasValue) ? insertMesWithSameContext : insertWithNewContext;

            var inserted = _connection.QuerySingle<(long id, long contextId)>(insertQuery, new { 
                text,
                chatId = telegramGptMessage.ChatId,
                contextId,
                telegramId = telegramGptMessage.TelegramId,
                replyToId,
                createdAt = DateTime.UtcNow,
                role,
                contextBound = telegramGptMessage.ContextBound
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

        public List<Message> GetRecentContextMessages(long userId, long chatId, long contextId)
        {
            var messages = _connection.Query<Message>(recentContextMessagesQuery, new {
                userId,
                chatId,
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
