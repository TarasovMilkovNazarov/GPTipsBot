using GPTipsBot.Api;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class MessageTypeHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<TelegramBotWorker> logger;

        public MessageTypeHandler(ITelegramBotClient botClient,
            MessageHandlerFactory messageHandlerFactory, ILogger<TelegramBotWorker> logger)
        {
            this.botClient = botClient;
            this.logger = logger;
            SetNextHandler(messageHandlerFactory.Create<GroupMessageHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Update.Message?.Type != Telegram.Bot.Types.Enums.MessageType.Text)
            {
                if (update.Update.Message?.Chat != null)
                {
                    await botClient.SendTextMessageAsync(update.Update.Message.Chat.Id,
                        BotResponse.OnlyMessagesAvailable, cancellationToken: update.CancellationToken);
                }
                
                return;
            }

            var message = update.Update.Message;

            // Only process text messages
            if (message.Text is not { } messageText)
                return;

            logger.LogInformation($"Received a '{messageText}' message in chat {message.Chat.Id}.");

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
