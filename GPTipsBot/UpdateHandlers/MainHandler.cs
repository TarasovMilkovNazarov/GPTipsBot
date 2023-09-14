using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Mapper;
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
        private readonly UserService userService;
        private readonly UnitOfWork unitOfWork;
        private readonly ILogger<MainHandler> logger;
        private readonly ITelegramBotClient botClient;

        public MainHandler(MessageHandlerFactory messageHandlerFactory, UnitOfWork unitOfWork, 
            ILogger<MainHandler> logger, ITelegramBotClient botClient, UserService userService)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.unitOfWork = unitOfWork;
            this.logger = logger;
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.Create<DeleteUserHandler>());
            this.userService = userService;
        }

        public override async Task HandleAsync(UpdateDecorator update, CancellationToken cancellationToken)
        {
            if (update.IsRecovered)
            {
                return;
            }

            if (update.ChatMemeberStatus == Telegram.Bot.Types.Enums.ChatMemberStatus.Kicked)
            {
                await base.HandleAsync(update, cancellationToken);
                return;
            }

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
            var settings = unitOfWork.BotSettings.Get(userKey.Id) ?? unitOfWork.BotSettings.Create(userKey.Id, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
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
