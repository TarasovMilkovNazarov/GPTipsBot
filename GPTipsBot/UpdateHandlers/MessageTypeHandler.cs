using GPTipsBot.Resources;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class MessageTypeHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<MessageTypeHandler> logger;

        public MessageTypeHandler(ITelegramBotClient botClient,
            HandlerFactory messageHandlerFactory, ILogger<MessageTypeHandler> logger)
        {
            this.botClient = botClient;
            this.logger = logger;
            SetNextHandler(messageHandlerFactory.Create<GroupMessageHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var mediaMessageTypes = new Telegram.Bot.Types.Enums.MessageType?[]{
                Telegram.Bot.Types.Enums.MessageType.Audio, 
                Telegram.Bot.Types.Enums.MessageType.Video,
                Telegram.Bot.Types.Enums.MessageType.Photo };

            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (mediaMessageTypes.Contains(update.Message?.Type))
            {
                await botClient.SendTextMessageAsync(update.ChatId, BotResponse.OnlyMessagesAvailable);
                
                return;
            }

            // Only process text messages
            if (update.Message?.Text is null)
                return;

            // Call next handler
            await base.HandleAsync(update);
        }
    }
}
