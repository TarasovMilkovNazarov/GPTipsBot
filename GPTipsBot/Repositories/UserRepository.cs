using Dapper;
using GPTipsBot.Db;
using Microsoft.Extensions.Logging;
using System.Data;
using User = GPTipsBot.Models.User;

namespace GPTipsBot.Repositories
{
    public class UserRepository : IDisposable
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<TelegramBotWorker> logger;
        private readonly string insertUserQuery = "INSERT INTO Users (id, firstname, lastname, createdat, isactive, source) " +
                "VALUES (@Id, @FirstName, @LastName, @CreatedAt, @IsActive, @Source) RETURNING id";
        private readonly string updateUserQuery = "Update Users SET isactive = 'true', source = @Source WHERE id = @telegramId;";
        private readonly string selectUserByTelegramId = $"SELECT * FROM Users WHERE id = @TelegramId;";
        private readonly string isUserExists = $"SELECT Count(*) FROM Users WHERE id = @TelegramId;";

        private readonly string removeUserQuery = "UPDATE users SET isactive = 'false' WHERE id = @telegramId;";

        public UserRepository(ILogger<TelegramBotWorker> logger, DapperContext context)
        {
            _connection = context.CreateConnection();
            _connection.Open();

            this.logger = logger;
        }

        public User? Get(long id)
        {
            return _connection.Query<User>(selectUserByTelegramId, new { telegramId = id }).FirstOrDefault();
        }

        public long Create(User user)
        {
            logger.LogInformation("CreateUser");
            _connection.QuerySingle<long>(insertUserQuery, user);

            return user.Id;
        }

        public void Update(User user)
        {
            var dbUser = Get(user.Id);
            if (dbUser == null)
            {
                throw new InvalidOperationException($"Can't find user with id {user.Id}");
            }

            _connection.ExecuteScalar(updateUserQuery, new
            {
                telegramId = user.Id,
                source = string.IsNullOrEmpty(dbUser.Source) ? user.Source : dbUser.Source
            });
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

        public long GetActiveUsersCount()
        {
            const string sql = @"SELECT Count(*) FROM Users WHERE isactive=true;";

            return _connection.QuerySingle<long>(sql);
        }

        public long SoftlyRemoveUser(long telegramId)
        {
            int count = _connection.ExecuteScalar<int>(isUserExists, new { telegramId });
            if (count == 0)
            {
                return -1;
            }

            _connection.Execute(removeUserQuery, new { telegramId });

            return telegramId;
        }

        public void Dispose()
        {
            _connection.Close(); // Close the connection when the service is disposed
            _connection.Dispose(); // Dispose the connection resources
        }
    }
}
