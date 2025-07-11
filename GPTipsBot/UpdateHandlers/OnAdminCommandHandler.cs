using GPTipsBot.Extensions;
using GPTipsBot.Resources;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class OnAdminCommandHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;

        public OnAdminCommandHandler(
            MessageHandlerFactory messageHandlerFactory,
            ITelegramBotClient botClient)
        {
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.GetRequiredService<RateLimitingHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            if (string.IsNullOrEmpty(update.Message.Text))
            {
                await base.HandleAsync(update);
                return;
            }

            var chatKey = update.UserChatKey;
            switch (update.Message.Text)
            {
                case "/fix" when chatKey.IsAdmin():
                {
                    var response = AppConfig.IsOnMaintenance ? BotResponse.Recovered : BotResponse.OnMaintenance;
                    AppConfig.IsOnMaintenance = !AppConfig.IsOnMaintenance;
                    await botClient.SendTextMessageAsync(chatKey.ChatId, response);
                    return;
                }
                case "/version" when chatKey.IsAdmin():
                    await botClient.SendBotVersionAsync(chatKey.ChatId);
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