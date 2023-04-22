using Dapper;
using GPTipsBot.Db;
using GPTipsBot.Dtos;

namespace GPTipsBot.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly DapperContext context;

        public UserRepository(DapperContext context)
        {
            this.context = context;
        }

        public long CreateUser(CreateEditUser user)
        {
            var query = "INSERT INTO Users (firstname, lastname, telegramid, timestamp, message) VALUES (@FirstName, @LastName, @TelegramId, @TimeStamp, @Message);" +
        "SELECT currval('users_id_seq')";
            string sql = $"SELECT COUNT(*) FROM Users WHERE TelegramId = @TelegramId";

            using (var connection = context.CreateConnection())
            {
                long id = 0;
                int count = connection.ExecuteScalar<int>(sql, user);
                if (count == 0)
                {
                    id = connection.QuerySingle<long>(query, user);
                }

                return id;
            }
        }
    }
}
