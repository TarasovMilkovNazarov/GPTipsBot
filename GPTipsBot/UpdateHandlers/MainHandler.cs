using GPTipsBot.Api;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Extensions;
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
        public static ConcurrentDictionary<long, UserStateEnum> userState = new ();
        private readonly UserRepository userRepository;
        private readonly ILogger<MainHandler> logger;

        public MainHandler(MessageHandlerFactory messageHandlerFactory, UserRepository userRepository, ILogger<MainHandler> logger)
        {
            this.userRepository = userRepository;
            this.logger = logger;
            SetNextHandler(messageHandlerFactory.Create<DeleteUserHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            if (update.TelegramGptMessage != null)
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
