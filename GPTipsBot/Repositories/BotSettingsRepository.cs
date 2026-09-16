using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Repositories
{
    public class BotSettingsRepository(ApplicationContext context, ILogger<BotSettingsRepository> logger)
        : GenericRepository<BotSettingsRepository>(context)
    {
        private readonly ApplicationContext _context = context;

        public BotSettings Create(long userId, string languageCode, string? preferredGptModel = null)
        {
            logger.LogInformation("Create settings userId={UserId} with language={Language}", userId, languageCode);

            var settings = new BotSettings
            {
                Id = userId,
                Language = languageCode,
                PreferredGptModel = preferredGptModel,
            };
            _context.BotSettings.Add(settings);

            return settings;
        }

        public BotSettings Update(long userId, string languageCode)
        {
            logger.LogInformation("Update settings userId={UserId} with culture={Language}", userId, languageCode);

            var settings = GetTracked(userId);
            if (settings == null)
            {
                return Create(userId, languageCode);
            }

            settings.Language = languageCode;
            return settings;
        }

        public BotSettings SetPreferredGptModel(long userId, string modelId, string fallbackLanguage = "ru")
        {
            logger.LogInformation("Set preferred model userId={UserId} model={ModelId}", userId, modelId);

            var settings = GetTracked(userId);
            if (settings == null)
            {
                return Create(userId, fallbackLanguage, modelId);
            }

            settings.PreferredGptModel = modelId;
            return settings;
        }

        public string GetPreferredGptModelId(long userId) =>
            GptModelCatalog.Resolve(Get(userId)?.PreferredGptModel).Id;

        public BotSettings SetPreferredPaymentProvider(
            long userId, PaymentProvider provider, string fallbackLanguage = "ru")
        {
            logger.LogInformation("Set preferred payment provider userId={UserId} provider={Provider}",
                userId, provider);

            var settings = GetTracked(userId);
            if (settings == null)
            {
                settings = Create(userId, fallbackLanguage);
            }

            settings.PreferredPaymentProvider = provider.ToString();
            return settings;
        }

        public PaymentProvider? GetPreferredPaymentProvider(long userId) =>
            Enum.TryParse<PaymentProvider>(Get(userId)?.PreferredPaymentProvider, out var provider)
                ? provider
                : null;

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

        private BotSettings? GetTracked(long userId) =>
            _context.BotSettings.FirstOrDefault(x => x.Id == userId);
    }
}
