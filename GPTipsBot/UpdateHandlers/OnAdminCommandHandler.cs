using GPTipsBot.Extensions;
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

        public OnAdminCommandHandler(
            MessageHandlerFactory messageHandlerFactory,
            ITelegramBotClient botClient,
            ImageCreatorService imageCreatorService)
        {
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.Create<RateLimitingHandler>());
            this.imageCreatorService = imageCreatorService;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            if (string.IsNullOrEmpty(update.Message.Text))
            {
                await base.HandleAsync(update);
                return;
            }

            var chatKey = update.UserChatKey;
            if (update.Message.Text == "/fix" && chatKey.IsAdmin() && AppConfig.IsOnMaintenance)
            {
                AppConfig.IsOnMaintenance = false;
                await botClient.SendTextMessageAsync(chatKey.ChatId, BotResponse.Recovered);
                return;
            }

            if (update.Message.Text.StartsWith("/updateBingCookie ") && chatKey.IsAdmin())
            {
                var cookie = update.Message.Text["/updateBingCookie ".Length..];
                imageCreatorService.UpdateBingCookies(cookie);
                await botClient.SendTextMessageAsync(chatKey.ChatId, BotResponse.CookiesUpdated, replyMarkup: new ReplyKeyboardRemove());
                return;
            }

            if (update.Message.Text.StartsWith("/version ") && chatKey.IsAdmin())
            {
                await botClient.SendTextMessageAsync(chatKey.ChatId, "Не умею ещё, но скоро научусь");
                return;
            }


            if (AppConfig.IsOnMaintenance)
            {
                await botClient.SendTextMessageAsync(chatKey.ChatId, BotResponse.OnMaintenance);
                return;
            }

            // Call next handler
            await base.HandleAsync(update);
        }
    }
}