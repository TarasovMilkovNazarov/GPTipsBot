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

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var telegramId = update.UserChatKey.Id;
            var chatId = update.UserChatKey.ChatId;

            if(!MessageService.UserToMessageCount.ContainsKey(telegramId))
            {
                MessageService.UserToMessageCount.Add(telegramId, 0);
            }

            MessageService.UserToMessageCount[telegramId] += 1;
            var mesCount = MessageService.UserToMessageCount[telegramId];
            
            if(mesCount > MessageService.MaxMessagesCountPerMinute)
            {
                return;
            }
            //send message for the first limit breaking
            else if(mesCount == MessageService.MaxMessagesCountPerMinute)
            {
                logger.LogError("Max messages limit reached");
                update.Reply.Text = string.Format(BotResponse.TooManyRequests, MessageService.ResettingInterval);
                await botClient.SendTextMessageAsync(chatId, update.Reply.Text);
                return;
            }

            await base.HandleAsync(update);
        }
    }
}
