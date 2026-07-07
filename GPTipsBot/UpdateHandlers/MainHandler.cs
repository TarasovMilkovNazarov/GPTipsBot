using System.Globalization;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class MainHandler
    {
        private readonly Dispatcher _dispatcher;
        private readonly SpeechToTextService _speechToTextService;
        private readonly TelejetAdClient _telejetAdClient;
        private readonly RateLimiter _rateLimiter;
        private readonly ITelegramBotClient _botClient;
        private readonly ApplicationContext _context;

        public static DateTime Start { get; private set; }

        public MainHandler(
            Dispatcher dispatcher,
            SpeechToTextService speechToTextService,
            TelejetAdClient telejetAdClient,
            RateLimiter rateLimiter,
            ITelegramBotClient botClient,
            ApplicationContext context)
        {
            _dispatcher = dispatcher;
            _botClient = botClient;
            _context = context;
            _speechToTextService = speechToTextService;
            _telejetAdClient = telejetAdClient;
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
                await _botClient.SendMessage(extendedUpd.UserChatKey.ChatId, BotResponse.OnMaintenance);
                return;
            }

            if (extendedUpd.IsRecovered)
            {
                return;
            }

            if (update.Message?.Voice != null)
            {
                try
                {
                    extendedUpd.Message.Text = await _speechToTextService.RecognizeVoice(update.Message.Voice.FileId);
                }
                catch (Exception)
                {
                    await _botClient.SendMessage(extendedUpd.UserChatKey.ChatId, BotResponse.SomethingWentWrong);
                    return;
                }
            }

            CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(extendedUpd.Language);

            try
            {
                await _dispatcher.HandleAsync(extendedUpd);
            }
            finally
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}