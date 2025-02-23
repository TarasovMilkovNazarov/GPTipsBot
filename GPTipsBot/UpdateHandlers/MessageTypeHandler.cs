using GPTipsBot.Resources;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.UpdateHandlers
{
    public class MessageTypeHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly ILogger<MessageTypeHandler> logger;

        public MessageTypeHandler(ITelegramBotClient botClient,
            MessageHandlerFactory messageHandlerFactory, ILogger<MessageTypeHandler> logger)
        {
            this.botClient = botClient;
            this.messageHandlerFactory = messageHandlerFactory;
            this.logger = logger;
            SetNextHandler(messageHandlerFactory.Create<GroupMessageHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var mediaMessageTypes = new MessageType?[]{
                MessageType.Audio,
                MessageType.Video };

            if (update.Message.Type == MessageType.Photo)
            {
                SetNextHandler(messageHandlerFactory.Create<ImageTextRecognitionHandler>());
            }

            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (mediaMessageTypes.Contains(update.Message?.Type))
            {
                await botClient.SendTextMessageAsync(update.ChatId, BotResponse.OnlyMessagesAvailable);
                
                return;
            }

            // Call next handler
            await base.HandleAsync(update);
        }
    }
}
