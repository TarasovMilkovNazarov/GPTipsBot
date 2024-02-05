using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot
{
    public class UpdateHandlerEntryPoint
    {
        private readonly MainHandler mainHandler;
        private readonly ITelegramBotClient telegramBotClient;
        private readonly SpeechToTextService speechToTextService;
        private readonly Bap bap;

        public static DateTime Start { get; private set; }

        public UpdateHandlerEntryPoint(MainHandler mainHandler, ITelegramBotClient telegramBotClient,
            SpeechToTextService speechToTextService, Bap bap)
        {
            this.mainHandler = mainHandler;
            this.telegramBotClient = telegramBotClient;
            this.speechToTextService = speechToTextService;
            this.bap = bap;
        }

        static UpdateHandlerEntryPoint()
        {
            Start = DateTime.UtcNow;
        }

        public async Task HandleUpdateAsync(Update update)
        {
            bap.HandleUpdate(update);
            if (bap.IsBapUpdate(update))
            {
                return;
            }

            if (update.Ignore())
                return;

            var extendedUpd = new UpdateDecorator(update);
            if (update.Message?.Voice != null)
            {
                extendedUpd.Message.Text = await speechToTextService.RecognizeVoice(update.Message.Voice.FileId);
            }

            CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(extendedUpd.Language);
            await mainHandler.HandleAsync(extendedUpd);
        }
    }
}