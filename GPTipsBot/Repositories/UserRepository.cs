﻿using Dapper;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Models;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ILogger<TelegramBotWorker> logger;
        private readonly DapperContext context;

        public UserRepository(ILogger<TelegramBotWorker> logger, DapperContext context)
        {
            this.logger = logger;
            this.context = context;
        }
        
        public long CreateUser(CreateEditUser user)
        {
            logger.LogInformation("CreateUser");

            var query = "INSERT INTO Users (firstname, lastname, telegramid, timestamp, message, isactive) " +
                "VALUES (@FirstName, @LastName, @TelegramId, @TimeStamp, @Message, @IsActive);" +
                "SELECT currval('users_id_seq')";
            string sql = $"SELECT COUNT(*) FROM Users WHERE TelegramId = @TelegramId;";

            using (var connection = context.CreateConnection())
            {
                long id = 0;
                int count = connection.ExecuteScalar<int>(sql, user);
                if (count == 0)
                {
                    id = connection.QuerySingle<long>(query, user);
                }

                var updateIsActiveQuery =  "Update Users SET isactive = 'true' WHERE TelegramId = @telegramId;";
                connection.ExecuteScalar(updateIsActiveQuery, user.TelegramId);

                return user.TelegramId;
            }
        }

        public IEnumerable<User> GetAll()
        {
            logger.LogInformation("GetAll");

            using (var connection = context.CreateConnection())
            {
                const string sql = @"SELECT * FROM Users";
                try
                {
                    connection.Query<User>(sql);
                }
                catch (Exception ex)
                {
                    logger.LogError($"GetAll error {ex.Message}");
                }

                return connection.Query<User>(sql);
            }
        }
        
        public long SoftlyRemoveUser(long telegramId)
        {
            var query = "Update Users set isactive = 'false' WHERE telegramid = @telegramId;";
            string sql = $"SELECT COUNT(*) FROM Users WHERE telegramid = @telegramId;";

            using (var connection = context.CreateConnection())
            {
                int count = connection.ExecuteScalar<int>(sql, telegramId);
                if (count == 0)
                {
                    return -1;
                }

                connection.Execute(query, telegramId);

                return telegramId;
            }
        }
    }
}
