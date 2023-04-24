using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<TelegramBotWorker> _logger;
        private readonly IConfiguration _configuration;
        public DapperContext(ILogger<TelegramBotWorker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }
        public IDbConnection CreateConnection()
        {
            if (AppConfig.Env == "Production")
            {
                var connectionString = Environment.GetEnvironmentVariable("PG_CONNECTION_STRING");
                _logger.LogInformation("Get postgres connection string: ", connectionString);

                return new NpgsqlConnection(Environment.GetEnvironmentVariable("PG_CONNECTION_STRING"));
            }

            return new NpgsqlConnection(_configuration["ConnectionString"]);
        }
    }
}
