using GPTipsBot.Api;
using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
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

        public override async Task HandleAsync(UpdateDecorator update, CancellationToken cancellationToken)
        {
            var telegramId = update.UserChatKey.Id;
            if (update.UserChatKey != null)
            {
                await base.HandleAsync(update, cancellationToken);
                return;
            }

            var chatId = update.UserChatKey?.ChatId;

            if (!MessageService.UserToMessageCount.TryGetValue(telegramId, out var mesCount))
            {
                MessageService.UserToMessageCount[telegramId] = 1;
            }
            else
            {
                if (mesCount + 1 > MessageService.MaxMessagesCountPerMinute)
                {
                    logger.LogError("Max messages limit reached");
                    await botClient.SendTextMessageAsync(chatId ?? telegramId, BotResponse.TooManyRequests, cancellationToken: cancellationToken);

                    return;
                }

                MessageService.UserToMessageCount[telegramId] += 1;
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
