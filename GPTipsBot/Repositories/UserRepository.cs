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
    public class UserRepository : IDisposable
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<TelegramBotWorker> logger;
        private readonly DapperContext context;
        private readonly MessageContextRepository messageRepository;
        private readonly string insertUserQuery = "INSERT INTO Users (id, firstname, lastname, createdat, isactive, source) " +
                "VALUES (@TelegramId, @FirstName, @LastName, @CreatedAt, @IsActive, @Source) RETURNING id";
        private readonly string updateUserQuery = "Update Users SET isactive = 'true', source = @Source WHERE id = @telegramId;";
        private readonly string selectUserByTelegramId = $"SELECT * FROM Users WHERE id = @TelegramId;";

        private readonly string removeUserQuery = "UPDATE users SET isactive = 'false' WHERE id = @telegramId;";

        public UserRepository(ILogger<TelegramBotWorker> logger, DapperContext context, MessageContextRepository messageRepository)
        {
            _connection = context.CreateConnection();
            _connection.Open();

            this.logger = logger;
            this.context = context;
            this.messageRepository = messageRepository;
        }

        public long CreateUpdateUser(TelegramGptMessage telegramGptMessage)
        {
            logger.LogInformation("CreateUser");

            User? dbUser;
            dbUser = _connection.Query<User>(selectUserByTelegramId, telegramGptMessage).FirstOrDefault();
            if (dbUser == null)
            {
                _connection.QuerySingle<long>(insertUserQuery, telegramGptMessage);
            }
            else
            {
                _connection.ExecuteScalar(updateUserQuery, new
                {
                    telegramId = telegramGptMessage.TelegramId,
                    source = string.IsNullOrEmpty(dbUser.Source) ? telegramGptMessage.Source : dbUser.Source
                });
            }

            return telegramGptMessage.TelegramId;
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
