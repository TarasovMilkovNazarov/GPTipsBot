using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using System.Globalization;
using GPTipsBot.Dtos;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot
{
    public class UpdateFirewall
    {
        private readonly MainHandler _mainHandler;
        private readonly SpeechToTextService _speechToTextService;
        private readonly TelejetAdClient _telejetAdClient;
        private readonly GramadsAdvertisementClient _gramadsAdvertisementClient;
        private readonly RateLimiter _rateLimiter;
        private readonly ITelegramBotClient _botClient;
        private static readonly object advertisementSyncObj = new();
        private static readonly HashSet<long> HamsterSent = new();

        public static DateTime Start { get; private set; }

        public UpdateFirewall(
            MainHandler mainHandler,
            SpeechToTextService speechToTextService,
            TelejetAdClient telejetAdClient,
            GramadsAdvertisementClient gramadsAdvertisementClient,
            RateLimiter rateLimiter,
            ITelegramBotClient botClient)
        {
            _mainHandler = mainHandler;
            _botClient = botClient;
            _speechToTextService = speechToTextService;
            _telejetAdClient = telejetAdClient;
            _gramadsAdvertisementClient = gramadsAdvertisementClient;
            _rateLimiter = rateLimiter;
            Start = DateTime.UtcNow;
        }

        public async Task HandleUpdateAsync(Update update)
        {
            var needHandleUpd = await _telejetAdClient.HandleUpdateAsync(update);
            if (!needHandleUpd)
            {
                return;
            }

            if (update.Ignore())
            {
                return;
            }

            var extendedUpd = new UpdateDecorator(update);

            if (!_rateLimiter.IsAllowed(extendedUpd))
            {
                return;
            }

            if (AppConfig.IsOnMaintenance)
            {
                await _botClient.SendTextMessageAsync(extendedUpd.UserChatKey.ChatId, BotResponse.OnMaintenance);
            }

            if (extendedUpd.IsRecovered)
            {
                return;
            }

            await _gramadsAdvertisementClient.SendPostToChat(extendedUpd.UserChatKey.ChatId);

            if (update.Message?.Voice != null)
            {
                extendedUpd.Message.Text = await _speechToTextService.RecognizeVoice(update.Message.Voice.FileId);
            }

            CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(extendedUpd.Language);

            SendHamsterAdvertisement(extendedUpd);

            await _mainHandler.HandleAsync(extendedUpd);
        }

        private void SendHamsterAdvertisement(UpdateDecorator extendedUpd)
        {
            var chatId = extendedUpd.UserChatKey.ChatId;

            lock (advertisementSyncObj)
            {
                if (HamsterSent.Add(chatId))
                {
                    _botClient.SendTextMessageAsync(chatId, BotResponse.Hamster, null, ParseMode.MarkdownV2).GetAwaiter().GetResult();
                }
            }
        }
    }
}