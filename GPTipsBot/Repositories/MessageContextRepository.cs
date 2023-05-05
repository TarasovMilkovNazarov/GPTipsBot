using Dapper;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
using GPTipsBot.Models;
using Microsoft.Extensions.Logging;
using System.Data;

namespace GPTipsBot.Repositories
{
    public class MessageContextRepository : IDisposable
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<TelegramBotWorker> logger;
        
        private readonly string insertMesWithContext = "INSERT INTO Messages (text, contextId, telegramId, chatId, timeStamp) " +
                "VALUES (@Text, @ContextId, @TelegramId, @ChatId, @TimeStamp) RETURNING id";
        private readonly string recentContextMessagesQuery = $"SELECT * FROM Messages WHERE contextid = @ContextId OrderBy TimeStamp desc Limit @Count;";
        private readonly string getLastMessage = $"SELECT * FROM Messages OrderBy TimeStamp DESC LIMIT 1;";
        private readonly string getLastContextId = $"SELECT contextid FROM Messages OrderBy ContextId DESC LIMIT 1;";
        private readonly string selectAllUserMessagesQuery = $"SELECT * FROM Messages WHERE TelegramId = @TelegramId;";

        public MessageContextRepository(ILogger<TelegramBotWorker> logger, DapperContext context)
        {
            _connection = context.CreateConnection();
            _connection.Open(); 

            this.logger = logger;
        }

        public long AddUserMessage(CreateEditUser dtoUser)
        {
            dtoUser.ContextId = GetLastContext(dtoUser.TelegramId);

            dtoUser.MessageId = _connection.QuerySingle<long>(insertMesWithContext, new { 
                text = dtoUser.Message, 
                userId = dtoUser.Id,
                dtoUser.ContextId,
                telegramId = dtoUser.TelegramId,
                timeStamp = DateTime.UtcNow
            });

            return dtoUser.MessageId;
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
        
        public long GetLastContext(long telegramId)
        {
            var context = _connection.QuerySingle<Message>(getLastMessage, new {
                telegramId
            });

            return context == null ? 0 : context.Id;
        }

        public long GetNextContextId()
        {
            var contextId = _connection.QuerySingle<long>(getLastContextId);

            return contextId+1;
        }

        public List<Message> GetRecentContextMessages(long contextId, int contextMessagesLimit = 5)
        {
            var messages = _connection.QuerySingle<List<Message>>(recentContextMessagesQuery, new {
                contextId,
                count = contextMessagesLimit
            });

            return messages;
        }

        public void ResetContext(CreateEditUser userDto, long chatId)
        {
            userDto.MessageId = _connection.QuerySingle<long>(insertMesWithContext, new { 
                text = "/reset", 
                userId = userDto.Id,
                contextId = 0,
                chatId,
                telegramId = userDto.TelegramId,
                timeStamp = DateTime.UtcNow
            });
        }

        public void Dispose()
        {
            _connection.Close(); // Close the connection when the service is disposed
            _connection.Dispose(); // Dispose the connection resources
        }
    }
}
