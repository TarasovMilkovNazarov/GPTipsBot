using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class MessageTypeHandler : BaseMessageHandler
    {
        private readonly ILogger<TelegramBotWorker> logger;

        public MessageTypeHandler(MessageHandlerFactory messageHandlerFactory, ILogger<TelegramBotWorker> logger)
        {
            this.logger = logger;
            SetNextHandler(messageHandlerFactory.Create<GroupMessageHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Update.Message is not { } message)
                return;
            // Only process text messages
            if (message.Text is not { } messageText)
                return;

            logger.LogInformation($"Received a '{messageText}' message in chat {message.Chat.Id}.");

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
