using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using System.Globalization;
using GPTipsBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot
{
    public class UpdateHandlerEntryPoint
    {
        private readonly MainHandler mainHandler;
        private readonly SpeechToTextService speechToTextService;
        private readonly TelejetAdClient telejetAdClient;
        private readonly GramadsAdvertisementClient _gramadsAdvertisementClient;
        private readonly BotSettingsRepository botSettingsRepository;
        private readonly ITelegramBotClient botClient;
        private static object advertisementSyncObj = new object();
        private static readonly HashSet<long> HamsterSent = new();

        public static DateTime Start { get; private set; }

        public UpdateHandlerEntryPoint(
            MainHandler mainHandler,
            SpeechToTextService speechToTextService,
            TelejetAdClient telejetAdClient,
            GramadsAdvertisementClient gramadsAdvertisementClient,
            BotSettingsRepository botSettingsRepository,
            ITelegramBotClient botClient)
        {
            this.mainHandler = mainHandler;
            this.botClient = botClient;
            this.speechToTextService = speechToTextService;
            this.telejetAdClient = telejetAdClient;
            _gramadsAdvertisementClient = gramadsAdvertisementClient;
            this.botSettingsRepository = botSettingsRepository;
            Start = DateTime.UtcNow;
        }

        public async Task HandleUpdateAsync(Update update)
        {
            var needHandleUpd = await telejetAdClient.HandleUpdateAsync(update);
            if (!needHandleUpd)
            {
                return;
            }

            if (update.Ignore())
            {
                return;
            }

            var extendedUpd = new UpdateDecorator(update);

            await _gramadsAdvertisementClient.SendPostToChat(extendedUpd.ChatId);

            if (update.Message?.Voice != null)
            {
                extendedUpd.Message.Text = await speechToTextService.RecognizeVoice(update.Message.Voice.FileId);
            }

            CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(extendedUpd.Language);

            SendHamsterAdvertisement(extendedUpd);

            await mainHandler.HandleAsync(extendedUpd);
        }

        private void SendHamsterAdvertisement(UpdateDecorator extendedUpd)
        {
            var chatId = extendedUpd.ChatId;

            lock (advertisementSyncObj)
            {
                if (HamsterSent.Add(chatId))
                {
                    botClient.SendTextMessageAsync(chatId, BotResponse.Hamster, null, ParseMode.MarkdownV2).GetAwaiter().GetResult();
                }
            }
        }
    }
}