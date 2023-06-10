﻿using GPTipsBot.Dtos;
using GPTipsBot.Repositories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace GPTipsBot.UpdateHandlers
{
    public class MainHandler : BaseMessageHandler
    {
        public static ConcurrentDictionary<UserKey, UserStateDto> userState = new ();
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly UserRepository userRepository;
        private readonly ILogger<MainHandler> logger;

        public MainHandler(MessageHandlerFactory messageHandlerFactory, UserRepository userRepository, ILogger<MainHandler> logger)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.userRepository = userRepository;
            this.logger = logger;
            SetNextHandler(messageHandlerFactory.Create<DeleteUserHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            if (update.Update.CallbackQuery != null)
            {
                SetNextHandler(messageHandlerFactory.Create<CommandHandler>());
                await base.HandleAsync(update, cancellationToken);
                return;
            }

            var userKey = update.TelegramGptMessage?.UserKey;
            if (userKey != null && !userState.ContainsKey(userKey))
            {
                userState.TryAdd(userKey, new UserStateDto(userKey));
            }
            if (update.Update.CallbackQuery == null && update.TelegramGptMessage != null)
            {
                userRepository.CreateUpdateUser(update.TelegramGptMessage);
            }

            // Call next handler
            try
            {
                await base.HandleAsync(update, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, null);
                return;
            }
            
        }
    }
}
