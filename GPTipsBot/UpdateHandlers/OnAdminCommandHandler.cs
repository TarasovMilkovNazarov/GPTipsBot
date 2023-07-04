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

        public override async Task HandleAsync(UpdateDecorator update, CancellationToken cancellationToken)
        {
            if (update.Message.Text == "/fix" && update.UserChatKey.Id == AppConfig.AdminId)
            {
                AppConfig.IsOnMaintenance = !AppConfig.IsOnMaintenance;
                if (!AppConfig.IsOnMaintenance)
                {
                    await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, BotResponse.Recovered, cancellationToken: cancellationToken);
                    return;
                }
            }
            if (update.Message.Text == "/switchProxy" && update.UserChatKey.Id == AppConfig.AdminId)
            {
                AppConfig.UseFreeApi = !AppConfig.UseFreeApi;
                await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, string.Format(BotResponse.SwitchProxy, AppConfig.UseFreeApi), cancellationToken: cancellationToken);
                return;
            }
            if(update.Message.Text.StartsWith("/updateBingCookie ") && update.UserChatKey.Id == AppConfig.AdminId)
            {
                var cookie = update.Message.Text.Substring("/updateBingCookie ".Length);
                imageCreatorService.UpdateBingCookies(cookie);
                await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, BotResponse.CookiesUpdated, cancellationToken: cancellationToken, replyMarkup: new ReplyKeyboardRemove());
                return;
            }

            if (AppConfig.IsOnMaintenance)
            {
                await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, BotResponse.OnMaintenance, cancellationToken: cancellationToken);
                return;
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
