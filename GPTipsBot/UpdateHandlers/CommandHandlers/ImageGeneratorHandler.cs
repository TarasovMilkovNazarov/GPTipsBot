using GPTipsBot.Repositories;

namespace GPTipsBot.UpdateHandlers
{
    public class SettingsCommandHandler : BaseMessageHandler
    {
        private readonly BotSettingsRepository botSettingsRepository;

        public SettingsCommandHandler(BotSettingsRepository botSettingsRepository)
        {
            this.botSettingsRepository = botSettingsRepository;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var userKey = update.UserChatKey;
            var langCode = Thread.CurrentThread.CurrentCulture.Name;

            var settings = botSettingsRepository.Get(userKey.Id);
            if (settings == null)
            {
                botSettingsRepository.Create(userKey.Id, langCode);
            }
            else
            {
                botSettingsRepository.Update(userKey.Id, langCode);
            }

            // Call next handler
            await base.HandleAsync(update);
        }
    }
}
