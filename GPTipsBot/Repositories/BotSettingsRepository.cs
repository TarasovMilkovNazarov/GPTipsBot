using GPTipsBot.Db;
using GPTipsBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Repositories
{
    public class BotSettingsRepository
    {
        private readonly ApplicationContext context;
        private readonly ILogger<BotSettingsRepository> logger;

        public BotSettingsRepository(ApplicationContext context, ILogger<BotSettingsRepository> logger)
        {
            this.context = context;
            this.logger = logger;
        }

        public BotSettings Create(long userId, string languageCode)
        {
            logger.LogInformation($"Create settings userId={userId} with language={languageCode}");

            var settings = new BotSettings() { Id = userId, Language = languageCode };
            context.BotSettings.Add(settings);

            return settings;
        }

        public BotSettings Update(long userId, string languageCode)
        {
            logger.LogInformation($"Update settings userId={userId} with culture={languageCode}");

            var settings = new BotSettings() { Id = userId, Language = languageCode };
            context.BotSettings.Update(settings);

            return settings;
        }

        public BotSettings? Get(long userId)
        {
            return context.BotSettings.AsNoTracking().FirstOrDefault(x => x.Id == userId);
        }
    }
}
