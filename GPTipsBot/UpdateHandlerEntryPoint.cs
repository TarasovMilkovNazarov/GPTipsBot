using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.UpdateHandlers;
using System.Globalization;
using Telegram.Bot.Types;

namespace GPTipsBot
{
    public class UpdateHandlerEntryPoint
    {
        private readonly MainHandler mainHandler;

        public static DateTime Start { get; private set; }

        public UpdateHandlerEntryPoint(MainHandler mainHandler)
        {
            this.mainHandler = mainHandler;
        }

        static UpdateHandlerEntryPoint()
        {
            Start = DateTime.UtcNow;
        }

        public async Task HandleUpdateAsync(Update update)
        {
            if (update.Ignore())
                return;

            var extendedUpd = new UpdateDecorator(update);

            CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(extendedUpd.Language);
            await mainHandler.HandleAsync(extendedUpd);
        }
    }
}