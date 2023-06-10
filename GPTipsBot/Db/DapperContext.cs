using Npgsql;
using System.Data;

namespace GPTipsBot.Db
{
    public class DapperContext
    {
        public IDbConnection CreateConnection() => new NpgsqlConnection(Config.ConnectionString);
    }
}
