using GPTipsBot.Api;
using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;

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

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var message = update.Update?.Message;

            if (message?.Text == "/fix" && message?.From?.Id == AppConfig.AdminId)
            {
                AppConfig.IsOnMaintenance = !AppConfig.IsOnMaintenance;
                if (!AppConfig.IsOnMaintenance)
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, Api.BotResponse.Recovered, cancellationToken: cancellationToken);
                    return;
                }
            }
            if (message?.Text == "/switchProxy" && message?.From?.Id == AppConfig.AdminId)
            {
                AppConfig.UseFreeApi = !AppConfig.UseFreeApi;
                await botClient.SendTextMessageAsync(message.Chat.Id, Api.BotResponse.SwitchProxy, cancellationToken: cancellationToken);
                return;
            }

            if (AppConfig.IsOnMaintenance)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, Api.BotResponse.OnMaintenance, cancellationToken: cancellationToken);
                return;
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
