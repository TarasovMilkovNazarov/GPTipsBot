using GPTipsBot.Api;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Extensions;
using GPTipsBot.Mapper;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.UpdateHandlers
{
    public class MainHandler : BaseMessageHandler
    {
        public static ConcurrentDictionary<UserChatKey, UserStateDto> userState = new ();
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
