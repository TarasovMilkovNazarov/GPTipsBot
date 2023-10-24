using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.Resources;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace GPTipsBot
{
    public class UpdateHandlerEntryPoint
    {
        private readonly ILogger<UpdateHandlerEntryPoint> logger;
        private readonly IServiceProvider serviceProvider;

        public static DateTime Start { get; private set; }

        public UpdateHandlerEntryPoint(ILogger<UpdateHandlerEntryPoint> logger, IServiceProvider serviceProvider)
        {
            this.logger = logger;
            this.serviceProvider = serviceProvider;
        }

        static UpdateHandlerEntryPoint()
        {
            Start = DateTime.UtcNow;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update)
        {
            if (update.Ignore())
                return;

            try
            {
                using var scope = serviceProvider.CreateScope();
                var mainHandler = scope.ServiceProvider.GetRequiredService<MainHandler>();
                var extendedUpd = new UpdateDecorator(update);

                CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(extendedUpd.Language);
                await mainHandler.HandleAsync(extendedUpd);
            }
            catch (ApiRequestException e)
            {
                logger.LogError(e, $"Telegram API Error:\n[{e.ErrorCode}]\n{e.Message}");
            }
            catch (Exception e)
            {
                logger.LogError(e, "{exMessage}. Update object from telegram: '{update}'", e.Message, update.Serialize());
                
                if (update.Message == null)
                    return;
                await botClient.SendTextMessageAsync(update.Message.Chat.Id, BotResponse.SomethingWentWrong);
            }
        }
    }
}
