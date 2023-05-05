using Dapper;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
using GPTipsBot.Models;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;

namespace GPTipsBot.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<TelegramBotWorker> logger;
        private readonly DapperContext context;
        private readonly MessageContextRepository messageRepository;
        private readonly string insertUserQuery = "INSERT INTO Users (firstname, lastname, telegramid, timestamp, isactive, source) " +
                "VALUES (@FirstName, @LastName, @TelegramId, @TimeStamp, @IsActive, @Source) RETURNING id";
        private readonly string insertMessageQuery = "INSERT INTO Messages (text, userId, telegramId) " +
                "VALUES (@Text, @UserId, @TelegramId) RETURNING id";
        private readonly string updateUserQuery = "Update Users SET isactive = 'true', messagescount = @messagesCount WHERE telegramid = @telegramId;";
        private readonly string selectUserByTelegramId = $"SELECT * FROM Users WHERE TelegramId = @TelegramId;";
        private readonly string selectAllUserMessagesQuery = $"SELECT * FROM Messages WHERE TelegramId = @TelegramId;";
        private readonly string removeUserQuery = "UPDATE users SET isactive = 'false' WHERE telegramid = @telegramId;";

        public UserRepository(ILogger<TelegramBotWorker> logger, DapperContext context, MessageContextRepository messageRepository)
        {
            _connection = context.CreateConnection();
            _connection.Open(); 

            this.logger = logger;
            this.context = context;
            this.messageRepository = messageRepository;
        }
        
        public long CreateUpdateUser(CreateEditUser dtoUser)
        {
            logger.LogInformation("CreateUser");

            User? dbUser;
            dbUser = _connection.Query<User>(selectUserByTelegramId, dtoUser).FirstOrDefault();
            if (dbUser == null)
            {
                dtoUser.Id = _connection.QuerySingle<long>(insertUserQuery, dtoUser);
            }
            else
            {
                dtoUser.Id = dbUser.Id;
                _connection.ExecuteScalar(updateUserQuery, new { 
                    telegramId = dtoUser.TelegramId,
                    messagesCount = ++dbUser.MessagesCount,
                    source = dtoUser.Source
                });
            }

            messageRepository.AddUserMessage(dtoUser);

            return dtoUser.Id;
        }

        public IEnumerable<User> GetAll()
        {
            const string sql = @"SELECT * FROM Users";
            IEnumerable<User> users = null;

            try
            {
                users = _connection.Query<User>(sql);
            }
            catch (Exception ex)
            {
                logger.LogWithStackTrace(LogLevel.Error, $"GetAll error {ex.Message}");
            }

            return users;
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
        
        public long SoftlyRemoveUser(long telegramId)
        {
            using (var connection = context.CreateConnection())
            {
                int count = connection.ExecuteScalar<int>(selectUserByTelegramId, new { telegramId });
                if (count == 0)
                {
                    return -1;
                }

                connection.Execute(removeUserQuery, new { telegramId });

                return telegramId;
            }
        }

        public void Dispose()
        {
            _connection.Close(); // Close the connection when the service is disposed
            _connection.Dispose(); // Dispose the connection resources
        }
    }
}
