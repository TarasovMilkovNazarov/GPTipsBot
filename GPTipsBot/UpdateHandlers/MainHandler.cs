﻿using GPTipsBot.Dtos;
using GPTipsBot.Mapper;
using GPTipsBot.Localization;
using GPTipsBot.Repositories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class MainHandler : BaseMessageHandler
    {
        public static ConcurrentDictionary<UserChatKey, UserStateDto> userState = new ();
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly UserRepository userRepository;
        private readonly BotSettingsRepository botSettingsRepository;
        private readonly ILogger<MainHandler> logger;
        private readonly ITelegramBotClient botClient;

        public MainHandler(MessageHandlerFactory messageHandlerFactory, UserRepository userRepository, 
            BotSettingsRepository botSettingsRepository, ILogger<MainHandler> logger, ITelegramBotClient botClient)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.userRepository = userRepository;
            this.botSettingsRepository = botSettingsRepository;
            this.logger = logger;
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.Create<DeleteUserHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update, CancellationToken cancellationToken)
        {
            if (update.CallbackQuery != null)
            {
                SetNextHandler(messageHandlerFactory.Create<CommandHandler>());
                await base.HandleAsync(update, cancellationToken);
                return;
            }

            var userKey = update.UserChatKey;
            if (userKey != null && !userState.ContainsKey(userKey))
            {
                userState.TryAdd(userKey, new UserStateDto(userKey));
            }
            if (update.CallbackQuery == null)
            {
                userRepository.CreateUpdateUser(UserMapper.Map(update.User));
                //var settings = botSettingsRepository.CreateUpdate(userKey.Id, update.Update.Message.From.LanguageCode);
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
