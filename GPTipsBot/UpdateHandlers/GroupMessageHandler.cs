﻿using GPTipsBot.Api;
using GPTipsBot.Dtos;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.UpdateHandlers
{
    public class GroupMessageHandler : BaseMessageHandler
    {
        public GroupMessageHandler(MessageHandlerFactory messageHandlerFactory)
        {
            SetNextHandler(messageHandlerFactory.Create<CommandHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var message = update.Update.Message;
            var botMentionedEntity = message?.EntityValues?.FirstOrDefault(ev => ev.ToLower().Contains("gptip"));
            var isBotMentioned = message.Entities?.FirstOrDefault()?.Type == MessageEntityType.Mention && botMentionedEntity != null;
            var groupOrChannelTypes = new ChatType?[] { ChatType.Supergroup, ChatType.Group, ChatType.Channel };
            var isGroupOrChannel = groupOrChannelTypes.Contains(message.Chat.Type);

            if (!isBotMentioned && isGroupOrChannel)
            {
                return;
            }
            if (isBotMentioned)
            {
                message.Text = message.Text.Substring(botMentionedEntity.Length).Trim();
                update.TelegramGptMessage.Message = message.Text;
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}