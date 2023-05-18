﻿using GPTipsBot.Api;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using System.Collections.Concurrent;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.UpdateHandlers
{
    public class MainHandler : BaseMessageHandler
    {
        public static ConcurrentDictionary<long, UserStateEnum> userState = new ();
        private readonly UserRepository userRepository;

        public MainHandler(MessageHandlerFactory messageHandlerFactory, UserRepository userRepository)
        {
            this.userRepository = userRepository;
            SetNextHandler(messageHandlerFactory.Create<DeleteUserHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            if (update.TelegramGptMessage != null)
            {
                userRepository.CreateUpdateUser(update.TelegramGptMessage);
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}