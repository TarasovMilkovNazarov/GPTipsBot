using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.Resources;
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
        private readonly TelejetAdClient telejetAdClient;
        private readonly GramadsAdvertisementClient _gramadsAdvertisementClient;
        private static object advertisementSyncObj = new object();
        private static readonly HashSet<long> HamsterSent = new();

        public static DateTime Start { get; private set; }

        public UpdateHandlerEntryPoint(
            MainHandler mainHandler,
            ITelegramBotClient telegramBotClient,
            SpeechToTextService speechToTextService,
            TelejetAdClient telejetAdClient,
            GramadsAdvertisementClient gramadsAdvertisementClient)
        {
            this.mainHandler = mainHandler;
            this.telegramBotClient = telegramBotClient;
            this.speechToTextService = speechToTextService;
            this.telejetAdClient = telejetAdClient;
            _gramadsAdvertisementClient = gramadsAdvertisementClient;
        }

        static UpdateHandlerEntryPoint()
        {
            Start = DateTime.UtcNow;
        }

        public async Task HandleUpdateAsync(Update update)
        {
            //PrometheusMetrics.ProcessedItemsCounter.Inc();
            //return;

            //dont remove! uncomment telejet advertisement later
            var needHandleUpd = await telejetAdClient.HandleUpdateAsync(update);
            if (!needHandleUpd)
            {
                return;
            }

            if (update.Ignore())
                return;

            var extendedUpd = new UpdateDecorator(update);

            await _gramadsAdvertisementClient.SendPostToChat(extendedUpd.ChatId);

            if (update.Message?.Voice != null)
            {
                extendedUpd.Message.Text = await speechToTextService.RecognizeVoice(update.Message.Voice.FileId);
            }

            CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(extendedUpd.Language);

            await mainHandler.HandleAsync(extendedUpd);
        }

        private void SendHamsterAdvertisement(UpdateDecorator extendedUpd)
        {
            var chatId = extendedUpd.ChatId;

            lock (advertisementSyncObj)
            {
                if (HamsterSent.Add(chatId))
                {
                    telegramBotClient.SendTextMessageAsync(chatId, BotResponse.Hamster);
                }
            }
        }
    }
}