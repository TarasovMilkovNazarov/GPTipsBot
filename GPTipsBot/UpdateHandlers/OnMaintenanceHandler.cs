using GPTipsBot.Api;
using GPTipsBot.Dtos;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class OnMaintenanceHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;

        public OnMaintenanceHandler(MessageHandlerFactory messageHandlerFactory, ITelegramBotClient botClient)
        {
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.Create<RateLimitingHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var message = update.Update.Message;

            if (message.Text == "/fix" && message.From.Id == AppConfig.AdminId)
            {
                AppConfig.IsOnMaintenance = !AppConfig.IsOnMaintenance;
                if (!AppConfig.IsOnMaintenance)
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, BotResponse.OnMaintenanceStop, cancellationToken: cancellationToken);
                    return;
                }
            }

            if (AppConfig.IsOnMaintenance)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, BotResponse.OnMaintenance, cancellationToken: cancellationToken);
                return;
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
