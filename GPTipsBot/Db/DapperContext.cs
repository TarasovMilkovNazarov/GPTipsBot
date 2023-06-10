using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Db
{
    public class DapperContext
    {
        public IDbConnection CreateConnection() => new NpgsqlConnection(Config.ConnectionString);
    }
}
