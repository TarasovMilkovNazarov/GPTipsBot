using Dapper;
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
        
        private readonly string insertUserQuery = "INSERT INTO Users (firstname, lastname, telegramid, timestamp, message, isactive, source) " +
                "VALUES (@FirstName, @LastName, @TelegramId, @TimeStamp, @Message, @IsActive, @Source);" +
                "SELECT currval('users_id_seq')";
        private readonly string updateUserQuery = "Update Users SET isactive = 'true', message = @message, messagescount = @messagesCount WHERE telegramid = @telegramId;";
        private readonly string selectUserByTelegramId = $"SELECT * FROM Users WHERE TelegramId = @TelegramId;";
        private readonly string isUserExistsQuery = "UPDATE users SET isactive = 'false' WHERE telegramid = @telegramId;";
        private readonly string removeUserQuery = "UPDATE users SET isactive = 'false' WHERE telegramid = @telegramId;";

        public UserRepository(ILogger<TelegramBotWorker> logger, DapperContext context)
        {
            this.logger = logger;
            this.context = context;
        }
        
        public long CreateUpdateUser(CreateEditUser dtoUser)
        {
            logger.LogInformation("CreateUser");

            using (var connection = context.CreateConnection())
            {
                User? dbUser;
                dbUser = connection.Query<User>(selectUserByTelegramId, dtoUser).FirstOrDefault();
                if (dbUser == null)
                {
                    dbUser = connection.QuerySingle<User>(insertUserQuery, dtoUser);

                    return dbUser.Id;
                }

                connection.ExecuteScalar(updateUserQuery, new { 
                    telegramId = dtoUser.TelegramId, 
                    message = dtoUser.Message, 
                    messagesCount = ++dbUser.MessagesCount,
                    source = dtoUser.Source
                });

                return dtoUser.TelegramId;
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
            using (var connection = context.CreateConnection())
            {
                int count = connection.ExecuteScalar<int>(isUserExistsQuery, new { telegramId });
                if (count == 0)
                {
                    return -1;
                }

                connection.Execute(removeUserQuery, new { telegramId });

                return telegramId;
            }
        }
    }
}
