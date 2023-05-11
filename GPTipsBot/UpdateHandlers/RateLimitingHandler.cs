using GPTipsBot.Api;
using GPTipsBot.Dtos;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class RateLimitingHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<TelegramBotWorker> logger;

        public RateLimitingHandler(MessageHandlerFactory messageHandlerFactory, ITelegramBotClient botClient, ILogger<TelegramBotWorker> logger)
        {
            this.botClient = botClient;
            this.logger = logger;

            SetNextHandler(messageHandlerFactory.Create<MessageTypeHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var telegramId = update.Update.Message.From.Id;
            var chatId = update.Update.Message.Chat.Id;

            if (!MessageService.UserToMessageCount.TryGetValue(telegramId, out var existingValue))
            {
                MessageService.UserToMessageCount[telegramId] = (1, DateTime.UtcNow);
            }
            else
            {
                if (existingValue.messageCount + 1 > MessageService.MaxMessagesCountPerMinute)
                {
                    logger.LogError("Max messages limit reached");
                    botClient.SendTextMessageAsync(chatId, BotResponse.TooManyRequests, cancellationToken: cancellationToken);

                    return;
                }

                MessageService.UserToMessageCount[telegramId] = (existingValue.messageCount++, DateTime.UtcNow);
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
