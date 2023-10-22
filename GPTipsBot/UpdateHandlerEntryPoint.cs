using GPTipsBot.Api;
using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.Resources;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace GPTipsBot
{
    public class UpdateHandlerEntryPoint
    {
        public Guid Guid { get; } = Guid.NewGuid();
        private readonly ILogger<UpdateHandlerEntryPoint> _logger;
        private readonly TelegramBotAPI telegramBotApi;
        private readonly IServiceProvider serviceProvider;

        public static DateTime Start { get; private set; }

        public UpdateHandlerEntryPoint(ILogger<UpdateHandlerEntryPoint> logger, ITelegramBotClient botClient,
            TelegramBotAPI telegramBotApi, IServiceProvider serviceProvider)
        {
            _logger = logger;
            this.telegramBotApi = telegramBotApi;
            this.serviceProvider = serviceProvider;
        }

        static UpdateHandlerEntryPoint()
        {
            Start = DateTime.UtcNow;
        }

        public async Task<UpdateDecorator?> HandleUpdateAsync(ITelegramBotClient botClient, Telegram.Bot.Types.Update update)
        {
            if (update.Ignore())
            {
                return null;
            }

            using var scope = serviceProvider.CreateScope();
            var mainHandler = scope.ServiceProvider.GetRequiredService<MainHandler>();

            UpdateDecorator extendedUpd = null;
            try
            {
                extendedUpd = new UpdateDecorator(update);

                CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(extendedUpd.Language);
                await mainHandler.HandleAsync(extendedUpd);

                return extendedUpd;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, update.Serialize());
                telegramBotApi.LogErrorMessageFromApiResponse(ex);
                if (ex is ApiRequestException || update.Message == null)
                {
                    return null;
                }

                await botClient.SendTextMessageAsync(update.Message.Chat.Id, BotResponse.SomethingWentWrong);
                return null;
            }
        }

        public async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception)
        {
            telegramBotApi.LogErrorMessageFromApiResponse(exception);
            // Cooldown in case of network connection error
            if (exception is RequestException)
                await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}
