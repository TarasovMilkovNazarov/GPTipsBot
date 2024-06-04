using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class RateLimitingHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<RateLimitingHandler> logger;
        private readonly RateLimitCache rateLimitCache;

        public RateLimitingHandler(
            MessageHandlerFactory messageHandlerFactory,
            ITelegramBotClient botClient,
            ILogger<RateLimitingHandler> logger,
            RateLimitCache rateLimitCache)
        {
            this.botClient = botClient;
            this.logger = logger;
            this.rateLimitCache = rateLimitCache;

            SetNextHandler(messageHandlerFactory.Create<MessageTypeHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var chatId = update.UserChatKey.ChatId;

            if (rateLimitCache.TryIncrementMessageCount(botClient, chatId))
            {
                await base.HandleAsync(update);
            }
        }
    }
}