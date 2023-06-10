using Dapper;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Mapper;
using GPTipsBot.Models;
using Microsoft.Extensions.Logging;
using System.Data;

namespace GPTipsBot.Repositories
{
    public class UserRepository : IDisposable
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<TelegramBotWorker> logger;
        private readonly DapperContext context;
        private readonly MessageRepository messageRepository;
        private readonly string insertUserQuery = "INSERT INTO Users (id, firstname, lastname, createdat, isactive, source) " +
                "VALUES (@Id, @FirstName, @LastName, @CreatedAt, @IsActive, @Source) RETURNING id";
        private readonly string updateUserQuery = "Update Users SET isactive = 'true', source = @Source WHERE id = @telegramId;";
        private readonly string selectUserByTelegramId = $"SELECT * FROM Users WHERE id = @TelegramId;";
        private readonly string isUserExists = $"SELECT Count(*) FROM Users WHERE id = @TelegramId;";

        private readonly string removeUserQuery = "UPDATE users SET isactive = 'false' WHERE id = @telegramId;";

        public UserRepository(ILogger<TelegramBotWorker> logger, DapperContext context, MessageRepository messageRepository)
        {
            _connection = context.CreateConnection();
            _connection.Open();

            this.logger = logger;
            this.context = context;
            this.messageRepository = messageRepository;
        }

        public long CreateUpdateUser(TelegramGptMessageUpdate telegramGptMessage)
        {
            logger.LogInformation("CreateUser");

            User? user;
            user = _connection.Query<User>(selectUserByTelegramId, new { telegramId = telegramGptMessage.UserKey.Id }).FirstOrDefault();
            if (user == null)
            {
                user = UserMapper.MapToUser(telegramGptMessage);
                _connection.QuerySingle<long>(insertUserQuery, user);
            }
            else
            {
                _connection.ExecuteScalar(updateUserQuery, new
                {
                    telegramId = telegramGptMessage.UserKey.Id,
                    source = string.IsNullOrEmpty(user.Source) ? telegramGptMessage.Source : user.Source
                });
            }

            return telegramGptMessage.UserKey.Id;
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
                logger.LogError(ex, null);
            }

            return users;
        }

        public long SoftlyRemoveUser(long telegramId)
        {
            using (var connection = context.CreateConnection())
            {
                int count = connection.ExecuteScalar<int>(isUserExists, new { telegramId });
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
