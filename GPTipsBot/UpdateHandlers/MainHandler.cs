using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Mapper;
using GPTipsBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using GPTipsBot.Models;
using GPTipsBot.Repositories;

namespace GPTipsBot.UpdateHandlers
{
    public class MainHandler : BaseMessageHandler
    {
        public static readonly ConcurrentDictionary<UserChatKey, UserStateDto> UserState = new ();
        private readonly UserService userService;
        private readonly UserCommandRepository userCommandRepository;
        private readonly ImageTextRecognitionHandler imageTextRecognitionHandler;
        private readonly ImageGeneratorHandler imageGeneratorHandler;
        private readonly CommandHandler commandHandler;
        private readonly UnitOfWork unitOfWork;
        private readonly ILogger<MainHandler> logger;

        public MainHandler(RecoveryHandler recoveryHandler, ImageTextRecognitionHandler imageTextRecognitionHandler,
            ImageGeneratorHandler imageGeneratorHandler, CommandHandler commandHandler, UnitOfWork unitOfWork,
            ILogger<MainHandler> logger, UserService userService, UserCommandRepository userCommandRepository)
        {
            this.imageTextRecognitionHandler = imageTextRecognitionHandler;
            this.imageGeneratorHandler = imageGeneratorHandler;
            this.commandHandler = commandHandler;
            this.unitOfWork = unitOfWork;
            this.logger = logger;
            this.userService = userService;
            this.userCommandRepository = userCommandRepository;
            SetNextHandler(recoveryHandler);
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            if (update.IsRecovered)
            {
                return;
            }

            var userKey = update.UserChatKey;

            if (!UserState.ContainsKey(userKey))
            {
                UserState.TryAdd(userKey, new UserStateDto(userKey));
            }

            var newUser = UserMapper.Map(update.User);
            try
            {
                userService.CreateUpdateUser(newUser);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Couldn't create user with telegramId {userId} in database", newUser.Id);
            }

            var language = unitOfWork.BotSettings.Get(userKey.Id)?.Language ?? update.Language;
            CultureInfo.CurrentUICulture = new CultureInfo(language);
            UserState[userKey].LanguageCode = language;

            var lastCommand = await userCommandRepository.GetLastAsync(update.UserChatKey);
            if (update.CallbackQuery != null || update.IsCommand)
            {
                SetNextHandler(commandHandler);
            }
            else if (!string.IsNullOrEmpty(update.Message.Text) && lastCommand?.Type == CommandType.Image)
            {
                SetNextHandler(imageGeneratorHandler);
            }
            else if (!string.IsNullOrEmpty(update.FileId))
            {
                SetNextHandler(imageTextRecognitionHandler);
            }

            // Call next handler
            try
            {
                await base.HandleAsync(update);
            }
            finally
            {
                unitOfWork.Save();
            }
        }
    }
}
