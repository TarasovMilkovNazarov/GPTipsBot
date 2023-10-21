using GPTipsBot.Resources;
using GPTipsBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers
{
    public class OnAdminCommandHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ImageCreatorService imageCreatorService;

        public OnAdminCommandHandler(MessageHandlerFactory messageHandlerFactory, ITelegramBotClient botClient, ImageCreatorService imageCreatorService)
        {
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.Create<RateLimitingHandler>());
            this.imageCreatorService = imageCreatorService;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            if (update.Message?.Text == null) {
                await base.HandleAsync(update);
                return;
            }

            if (update.Message.Text == "/fix" && update.UserChatKey.Id == AppConfig.AdminId)
            {
                AppConfig.IsOnMaintenance = !AppConfig.IsOnMaintenance;
                if (!AppConfig.IsOnMaintenance)
                {
                    await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, BotResponse.Recovered);
                    return;
                }
            }

            if(update.Message.Text.StartsWith("/updateBingCookie ") && update.UserChatKey.Id == AppConfig.AdminId)
            {
                var cookie = update.Message.Text.Substring("/updateBingCookie ".Length);
                imageCreatorService.UpdateBingCookies(cookie);
                await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, BotResponse.CookiesUpdated, replyMarkup: new ReplyKeyboardRemove());
                return;
            }

            if (AppConfig.IsOnMaintenance)
            {
                await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, BotResponse.OnMaintenance);
                return;
            }

            // Call next handler
            await base.HandleAsync(update);
        }
    }
}
