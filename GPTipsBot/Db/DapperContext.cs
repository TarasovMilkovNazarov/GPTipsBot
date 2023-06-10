using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;

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

                return new NpgsqlConnection(connectionString);
            }

            return new NpgsqlConnection(_configuration["ConnectionString"]);
        }
    }
}
