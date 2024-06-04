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
            SetNextHandler(messageHandlerFactory.Create<RateLimitingHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            if (string.IsNullOrEmpty(update.Message.Text))
            {
                await base.HandleAsync(update);
                return;
            }

            var chatKey = update.UserChatKey;
            if (update.Message.Text == "/fix" && chatKey.IsAdmin())
            {
                string response;
                if (AppConfig.IsOnMaintenance)
                {
                    response = BotResponse.Recovered;
                }
                else
                {
                    response = BotResponse.OnMaintenance;
                }

                AppConfig.IsOnMaintenance = !AppConfig.IsOnMaintenance;

                await botClient.SendTextMessageAsync(chatKey.ChatId, response);
                return;
            }

            if (update.Message.Text == "/version" && chatKey.IsAdmin())
            {
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