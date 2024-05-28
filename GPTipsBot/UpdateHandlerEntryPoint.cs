using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Prometheus;
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
        private readonly TelejetAdClient telejetAdClient;

        public static DateTime Start { get; private set; }

        public UpdateHandlerEntryPoint(MainHandler mainHandler, ITelegramBotClient telegramBotClient,
            SpeechToTextService speechToTextService, TelejetAdClient telejetAdClient)
        {
            this.mainHandler = mainHandler;
            this.telegramBotClient = telegramBotClient;
            this.speechToTextService = speechToTextService;
            this.telejetAdClient = telejetAdClient;
        }

        static UpdateHandlerEntryPoint()
        {
            Start = DateTime.UtcNow;
        }

        public async Task HandleUpdateAsync(Update update)
        {
            //PrometheusMetrics.ProcessedItemsCounter.Inc();
            //return;
            var needHandleUpd = await telejetAdClient.HandleUpdateAsync(update);
            if (!needHandleUpd)
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