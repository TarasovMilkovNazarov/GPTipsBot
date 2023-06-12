using GPTipsBot.Resources;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class OnAdminCommandHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;

        public OnAdminCommandHandler(MessageHandlerFactory messageHandlerFactory, ITelegramBotClient botClient)
        {
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.Create<RateLimitingHandler>());
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
