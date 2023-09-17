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
    public partial class UpdateHandlerEntryPoint : IUpdateHandler
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
            Start = DateTime.UtcNow;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Telegram.Bot.Types.Update update, CancellationToken cancellationToken)
        {
            using var scope = serviceProvider.CreateScope();
            var mainHandler = scope.ServiceProvider.GetRequiredService<MainHandler>();

            if (update.MyChatMember?.OldChatMember.Status == ChatMemberStatus.Left && update.MyChatMember?.NewChatMember.Status == ChatMemberStatus.Member)
            {
                return;
            }

            var extendedUpd = new UpdateDecorator(update, cancellationToken);

            var userLang = extendedUpd.GetUserLanguage();
            CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(userLang);

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
