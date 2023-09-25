using GPTipsBot.Api;
using GPTipsBot.Localization;
using GPTipsBot.Resources;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

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

        public async Task<UpdateDecorator?> HandleUpdateAsync(ITelegramBotClient botClient, Telegram.Bot.Types.Update update, CancellationToken cancellationToken)
        {
            using var scope = serviceProvider.CreateScope();
            var mainHandler = scope.ServiceProvider.GetRequiredService<MainHandler>();

            if (update.MyChatMember?.OldChatMember.Status == ChatMemberStatus.Left && update.MyChatMember?.NewChatMember.Status == ChatMemberStatus.Member)
            {
                return null;
            }

            try
            {
                var extendedUpd = new UpdateDecorator(update, cancellationToken);

                CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(extendedUpd.Language);
                await mainHandler.HandleAsync(extendedUpd,cancellationToken);

                return extendedUpd;
            }
            catch (Exception ex)
            {
                telegramBotApi.LogErrorMessageFromApiResponse(ex);
                if (ex is ApiRequestException apiEx || update.Message == null)
                {
                    return null;
                }

                await botClient.SendTextMessageAsync(update.Message.Chat.Id, BotResponse.SomethingWentWrong, cancellationToken: cancellationToken);
                return null;
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
