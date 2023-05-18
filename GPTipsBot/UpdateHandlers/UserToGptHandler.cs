﻿using GPTipsBot.Api;
using GPTipsBot.Enums;
using GPTipsBot.Repositories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class UserToGptHandler : BaseMessageHandler
    {
        private readonly MessageContextRepository messageRepository;
        private readonly GptAPI gptAPI;
        private readonly ActionStatus typingStatus;
        private readonly ILogger<UserToGptHandler> logger;

        public UserToGptHandler(MessageHandlerFactory messageHandlerFactory, MessageContextRepository messageRepository, 
            GptAPI gptAPI, ActionStatus typingStatus, ILogger<UserToGptHandler> logger)
        {
            this.messageRepository = messageRepository;
            this.gptAPI = gptAPI;
            this.typingStatus = typingStatus;
            this.logger = logger;
            SetNextHandler(messageHandlerFactory.Create<GptToUserHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var message = update.TelegramGptMessage;

            await typingStatus.Start(update.Update.Message.Chat.Id, Telegram.Bot.Types.Enums.ChatAction.Typing, cancellationToken);
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                var gtpResponse = await gptAPI.SendMessage(message);
                sw.Stop();
                logger.LogInformation($"Get response to message {message.MessageId} takes {sw.Elapsed.TotalSeconds}s");
                message.Reply = gtpResponse.text;
                messageRepository.AddBotResponse(message);
            }
            finally
            {
                await typingStatus.Stop(cancellationToken);
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}