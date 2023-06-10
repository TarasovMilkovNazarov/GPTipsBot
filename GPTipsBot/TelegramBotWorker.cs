using GPTipsBot.Api;
using GPTipsBot.Localization;
using GPTipsBot.Resources;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;

namespace GPTipsBot
{
    public partial class TelegramBotWorker : IUpdateHandler
    {
        private readonly ILogger<TelegramBotWorker> _logger;
        private readonly GptAPI gptAPI;
        private readonly TelegramBotAPI telegramBotApi;
        private readonly MessageHandlerFactory messageHandlerFactory;

        public static readonly Dictionary<long, Queue<Telegram.Bot.Types.Update>> userToUpdatesQueue = new Dictionary<long, Queue<Telegram.Bot.Types.Update>>();
        public static DateTime Start { get; private set; }

        public TelegramBotWorker(ILogger<TelegramBotWorker> logger, GptAPI gptAPI, 
            TelegramBotAPI telegramBotApi, MessageHandlerFactory messageHandlerFactory)
        {
            _logger = logger;
            this.gptAPI = gptAPI;
            this.telegramBotApi = telegramBotApi;
            this.messageHandlerFactory = messageHandlerFactory;
            Start = DateTime.UtcNow;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Telegram.Bot.Types.Update update, CancellationToken cancellationToken)
        {
            var userCulture = update.Message?.From?.LanguageCode;
            CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(userCulture);

            var mainHandler = messageHandlerFactory.Create<MainHandler>();
            var extendedUpd = new UpdateDecorator(update, cancellationToken);

            try
            {
                await mainHandler.HandleAsync(extendedUpd, cancellationToken);
            }
            catch (Exception ex)
            {
                telegramBotApi.LogErrorMessageFromApiResponse(ex);
                if (ex is ApiRequestException apiEx || update.Message == null)
                {
                    return;
                }

                await botClient.SendTextMessageAsync(update.Message.Chat.Id, BotResponse.SomethingWentWrong, cancellationToken: cancellationToken);
            }
        }

        public async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            telegramBotApi.LogErrorMessageFromApiResponse(exception);
            // Cooldown in case of network connection error
            if (exception is RequestException)
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
}
