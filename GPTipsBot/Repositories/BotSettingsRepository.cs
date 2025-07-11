using GPTipsBot.Db;
using GPTipsBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Repositories
{
    public class BotSettingsRepository : GenericRepository<BotSettingsRepository>
    {
        private readonly ApplicationContext _context;
        private readonly ILogger<BotSettingsRepository> _logger;

        public BotSettingsRepository(ApplicationContext context, ILogger<BotSettingsRepository> logger) : base(context)
        {
            _context = context;
            _logger = logger;
        }

        public BotSettings Create(long userId, string languageCode)
        {
            _logger.LogInformation($"Create settings userId={userId} with language={languageCode}");

            var settings = new BotSettings() { Id = userId, Language = languageCode };
            _context.BotSettings.Add(settings);

            return settings;
        }

        public BotSettings Update(long userId, string languageCode)
        {
            _logger.LogInformation($"Update settings userId={userId} with culture={languageCode}");

            var settings = new BotSettings() { Id = userId, Language = languageCode };
            _context.BotSettings.Update(settings);

            return settings;
        }

        public void Delete(long id)
        {
            var settings = _context.BotSettings.FirstOrDefault(x => x.Id == id);

            if (settings == null)
            {
                throw new Exception($"Settings id={id} not found");
            }

            _context.BotSettings.Remove(settings);
        }

        public BotSettings? Get(long userId)
        {
            return _context.BotSettings.AsNoTracking().FirstOrDefault(x => x.Id == userId);
        }
    }
}
