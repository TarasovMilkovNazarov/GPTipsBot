using Dapper;
using GPTipsBot.Db;
using GPTipsBot.Models;
using Microsoft.Extensions.Logging;
using System.Data;

namespace GPTipsBot.Repositories
{
    public class BotSettingsRepository : IDisposable
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<BotSettingsRepository> logger;
        private readonly DapperContext context;
        private readonly string getSettings = $"SELECT * FROM BotSettings WHERE id = @UserId;";
        private readonly string insertSetting = "INSERT INTO BotSettings (id, culture) " +
                "VALUES (@Id, @Culture) RETURNING id";
        private readonly string updateSettingsQuery = "Update BotSettings SET culture = @Culture WHERE userId = @UserId;";

        public BotSettingsRepository(ILogger<BotSettingsRepository> logger, DapperContext context)
        {
            _connection = context.CreateConnection();
            _connection.Open();

            this.logger = logger;
            this.context = context;
        }

        public BotSettings CreateUpdate(long userId, string culture)
        {
            logger.LogInformation("CreateUser");

            BotSettings? settings = _connection.Query<BotSettings>(getSettings, new { userId }).FirstOrDefault();

            if (settings == null)
            {
                settings = _connection.QuerySingle<BotSettings>(insertSetting, new
                {
                    userId,
                    culture
                });

                return settings;
            }

            _connection.ExecuteScalar(updateSettingsQuery, new
            {
                userId,
                culture
            });

            return settings;
        }

        public BotSettings Get(long userId)
        {
            var settings = _connection.Query<BotSettings>(getSettings, new { userId }).FirstOrDefault();

            return settings;
        }

        public void Dispose()
        {
            _connection.Close(); // Close the connection when the service is disposed
            _connection.Dispose(); // Dispose the connection resources
        }
    }
}
