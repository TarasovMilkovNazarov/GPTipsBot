using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class RateLimitingHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<RateLimitingHandler> logger;

        public RateLimitingHandler(MessageHandlerFactory messageHandlerFactory, ITelegramBotClient botClient, ILogger<RateLimitingHandler> logger)
        {
            this.botClient = botClient;
            this.logger = logger;

            SetNextHandler(messageHandlerFactory.Create<MessageTypeHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update, CancellationToken cancellationToken)
        {
            var telegramId = update.UserChatKey.Id;
            var chatId = update.UserChatKey.ChatId;

            if (!MessageService.UserToMessageCount.TryGetValue(telegramId, out var mesCount))
            {
                MessageService.UserToMessageCount[telegramId] = 1;
            }
            else
            {
                if (mesCount + 1 > MessageService.MaxMessagesCountPerMinute)
                {
                    logger.LogError("Max messages limit reached");
                    update.Reply.Text = string.Format(BotResponse.TooManyRequests, MessageService.ResettingInterval);

                    await botClient.SendTextMessageAsync(chatId, update.Reply.Text, cancellationToken: cancellationToken);

                    return;
                }

                MessageService.UserToMessageCount[telegramId] += 1;
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
