﻿using GPTipsBot.Api;
using GPTipsBot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class GptToUserHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;

        public GptToUserHandler(ITelegramBotClient botClient)
        {
            this.botClient = botClient;
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var message = update.TelegramGptMessage;

            if (string.IsNullOrEmpty(update.TelegramGptMessage.Reply))
            {
                await botClient.SendTextMessageAsync(message.UserKey.ChatId, BotResponse.SomethingWentWrong, cancellationToken: update.CancellationToken);
                return;
            }

            await botClient.SendTextMessageAsync(message.UserKey.ChatId, message.Reply, 
                replyToMessageId: (int)message.TelegramMessageId, cancellationToken: update.CancellationToken);

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
