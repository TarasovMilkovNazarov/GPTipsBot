using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.Resources;
using GPTipsBot.UpdateHandlers;
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
        private readonly MainHandler mainHandler;

        public static DateTime Start { get; private set; }

        public UpdateHandlerEntryPoint(ILogger<UpdateHandlerEntryPoint> logger, MainHandler mainHandler)
        {
            this.logger = logger;
            this.mainHandler = mainHandler;
        }

        static UpdateHandlerEntryPoint()
        {
            Start = DateTime.UtcNow;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update)
        {
            try
            {
                if (update.Ignore())
                    return;
                
                var extendedUpd = new UpdateDecorator(update);

                CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(extendedUpd.Language);
                await mainHandler.HandleAsync(extendedUpd);
            }
            catch (ApiRequestException e)
            {
                logger.LogError(e, "Telegram API Error [{Code}] {Message}", e.ErrorCode, e.Message);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while handling update. Update details: text: '{UpdateText}', username: '{UserName}', updateId: '{UpdateId}', chatId: '{ChatId}'", 
                    update.Message?.Text, update.Message?.Chat.Username, update.Id, update.Message?.Chat.Id);

                if (update.Message == null)
                    return;
                
                await botClient.SendTextMessageAsync(update.Message.Chat.Id, BotResponse.SomethingWentWrong);
            }
        }
    }
}