using Dapper;
using GPTipsBot.Db;
using GPTipsBot.Models;
using Microsoft.Extensions.Logging;
using System.Data;
using static Dapper.SqlMapper;

namespace GPTipsBot.Repositories
{
    public class BotSettingsRepository : IDisposable
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<BotSettingsRepository> logger;
        private readonly string getSettings = "SELECT * FROM BotSettings WHERE id = @UserId;";
        private readonly string insertSetting = "INSERT INTO BotSettings (id, language) VALUES (@UserId, @language) RETURNING id, language;";
        private readonly string updateSettingsQuery = "Update BotSettings SET language = @language WHERE id = @UserId RETURNING id, language;";

        public BotSettingsRepository(ILogger<BotSettingsRepository> logger, DapperContext context)
        {
            _connection = context.CreateConnection();
            _connection.Open();

            this.logger = logger;
        }

        public BotSettings Create(long userId, string languageCode)
        {
            logger.LogInformation($"Create settings userId={userId} with language={languageCode}");

            var settings = _connection.QuerySingle<BotSettings>(insertSetting, new
            {
                userId,
                language = languageCode
            });

            return settings;
        }

        public BotSettings Update(long userId, string languageCode)
        {
            logger.LogInformation($"Update settings userId={userId} with culture={languageCode}");

            var settings = _connection.ExecuteScalar<BotSettings>(updateSettingsQuery, new
            {
                userId,
                language = languageCode
            });

            return settings;
        }

        public BotSettings Get(long userId)
        {
            var settings = _connection.QueryFirstOrDefault<BotSettings>(getSettings, new { userId });

            return settings;
        }

        public void Dispose()
        {
            _connection.Close(); // Close the connection when the service is disposed
            _connection.Dispose(); // Dispose the connection resources
        }
    }
}
