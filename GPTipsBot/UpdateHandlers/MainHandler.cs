using GPTipsBot.Dtos;
using GPTipsBot.Mapper;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class MainHandler : BaseMessageHandler
    {
        public static ConcurrentDictionary<UserChatKey, UserStateDto> userState = new ();
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly UserRepository userRepository;
        private readonly UserService userService;
        private readonly BotSettingsRepository botSettingsRepository;
        private readonly ILogger<MainHandler> logger;
        private readonly ITelegramBotClient botClient;

        public MainHandler(MessageHandlerFactory messageHandlerFactory, UserRepository userRepository, 
            BotSettingsRepository botSettingsRepository, ILogger<MainHandler> logger, ITelegramBotClient botClient, UserService userService)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.userRepository = userRepository;
            this.botSettingsRepository = botSettingsRepository;
            this.logger = logger;
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.Create<DeleteUserHandler>());
            this.userService = userService;
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

            if (!userState.ContainsKey(userKey))
            {
                userState.TryAdd(userKey, new UserStateDto(userKey));
            }

            userService.CreateUpdateUser(UserMapper.Map(update.User));
            var settings = botSettingsRepository.Get(userKey.Id) ?? botSettingsRepository.Create(userKey.Id, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            CultureInfo.CurrentUICulture = new CultureInfo(settings.Language);
            userState[userKey].LanguageCode = settings.Language;

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
