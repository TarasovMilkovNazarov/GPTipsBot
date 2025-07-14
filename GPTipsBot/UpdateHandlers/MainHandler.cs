using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Mapper;
using GPTipsBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using GPTipsBot.Extensions;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class MainHandler : BaseMessageHandler
    {
        public static readonly ConcurrentDictionary<UserChatKey, UserStateDto> UserState = new ();
        private readonly UserService _userService;
        private readonly UserCommandRepository _userCommandRepository;
        private readonly MoneyService _moneyService;
        private readonly ITelegramBotClient _botClient;
        private readonly RecoveryNotificationHandler _recoveryNotificationHandler;
        private readonly ImageTextRecognitionHandler _imageTextRecognitionHandler;
        private readonly ImageGeneratorHandler _imageGeneratorHandler;
        private readonly CommandHandler _commandHandler;
        private readonly ChatGptHandler _chatGptHandler;
        private readonly AdminCommandHandler _adminCommandHandler;
        private readonly UnitOfWork _unitOfWork;
        private readonly ILogger<MainHandler> _logger;

        public MainHandler(
            ITelegramBotClient botClient,
            RecoveryNotificationHandler recoveryNotificationHandler,
            ImageTextRecognitionHandler imageTextRecognitionHandler,
            ImageGeneratorHandler imageGeneratorHandler, CommandHandler commandHandler,
            ChatGptHandler chatGptHandler,
            AdminCommandHandler adminCommandHandler, UnitOfWork unitOfWork,
            ILogger<MainHandler> logger, UserService userService, UserCommandRepository userCommandRepository,
            MoneyService moneyService)
        {
            _botClient = botClient;
            _recoveryNotificationHandler = recoveryNotificationHandler;
            _imageTextRecognitionHandler = imageTextRecognitionHandler;
            _imageGeneratorHandler = imageGeneratorHandler;
            _commandHandler = commandHandler;
            _chatGptHandler = chatGptHandler;
            _adminCommandHandler = adminCommandHandler;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _userService = userService;
            _userCommandRepository = userCommandRepository;
            _moneyService = moneyService;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var userKey = update.UserChatKey;

            if (!UserState.ContainsKey(userKey))
            {
                UserState.TryAdd(userKey, new UserStateDto());
            }

            var newUser = UserMapper.Map(update.User);
            try
            {
                _userService.CreateUpdateUser(newUser);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Couldn't create user with telegramId {userId} in database", newUser.Id);
            }

            var language = _unitOfWork.BotSettings.Get(userKey.Id)?.Language ?? update.Language;
            CultureInfo.CurrentUICulture = new CultureInfo(language);

            if (update.PreCheckoutQuery != null)
            {
                await _moneyService.AddMoneyAsync(update.UserChatKey.Id, update.PreCheckoutQuery.TotalAmount,
                    "TRX", CancellationToken.None);
                return;
                await _botClient.AnswerPreCheckoutQueryAsync(
                    preCheckoutQueryId: update.PreCheckoutQuery.Id);
            }

            if (update.Message?.SuccessfulPayment != null)
            {
                await _botClient.SendTextMessageAsync(
                    chatId: update.UserChatKey.ChatId,
                    text: "Thank you for your payment! Your premium access has been activated.");
            }

            var lastCommand = await _userCommandRepository.GetLastAsync(update.UserChatKey);
            if (update.IsAdminCommand())
            {
                SetNextHandler(_adminCommandHandler);
            }
            else if (update.IsExpired())
            {
                SetNextHandler(_recoveryNotificationHandler);
            }
            else if (update.CallbackQuery != null || update.IsCommand)
            {
                SetNextHandler(_commandHandler);
            }
            else if (!string.IsNullOrEmpty(update.Message.Text) && lastCommand?.Type == CommandType.Image)
            {
                SetNextHandler(_imageGeneratorHandler);
            }
            else if (!string.IsNullOrEmpty(update.FileId))
            {
                SetNextHandler(_imageTextRecognitionHandler);
            }
            else
            {
                SetNextHandler(_chatGptHandler);
            }

            try
            {
                await base.HandleAsync(update);
            }
            finally
            {
                _unitOfWork.Save();
            }
        }
    }
}
