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

            SetNextHandler(messageHandlerFactory.Create<GroupMessageHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var telegramId = update.Update.Message?.From?.Id;
            if (!telegramId.HasValue)
            {
                await base.HandleAsync(update, cancellationToken);
                return;
            }

            var chatId = update.Update.Message?.Chat.Id;

            if (!MessageService.UserToMessageCount.TryGetValue(telegramId.Value, out var existingValue))
            {
                MessageService.UserToMessageCount[telegramId.Value] = (1, DateTime.UtcNow);
            }
            else
            {
                if (existingValue.messageCount + 1 > MessageService.MaxMessagesCountPerMinute)
                {
                    logger.LogError("Max messages limit reached");
                    await botClient.SendTextMessageAsync(chatId ?? telegramId.Value, BotResponse.TooManyRequests, cancellationToken: cancellationToken);

                    return;
                }

                MessageService.UserToMessageCount[telegramId.Value] = (existingValue.messageCount++, DateTime.UtcNow);
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
