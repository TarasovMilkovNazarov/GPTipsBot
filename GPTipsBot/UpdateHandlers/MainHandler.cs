using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Mapper;
using GPTipsBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers
{
    public class MainHandler : BaseMessageHandler
    {
        public static readonly ConcurrentDictionary<UserChatKey, UserStateDto> UserState = new ();
        private readonly UserService _userService;
        private readonly UserCommandRepository _userCommandRepository;
        private readonly MoneyService _moneyService;
        private readonly ApplicationContext _context;
        private readonly BotSettingsRepository _botSettingsRepository;
        private readonly InvoiceRepository _invoiceRepository;
        private readonly IGpt _gptService;
        private readonly ITelegramBotClient _botClient;
        private readonly RecoveryNotificationHandler _recoveryNotificationHandler;
        private readonly ImageTextRecognitionHandler _imageTextRecognitionHandler;
        private readonly ImageGeneratorHandler _imageGeneratorHandler;
        private readonly CommandHandler _commandHandler;
        private readonly ChatGptHandler _chatGptHandler;
        private readonly AdminCommandHandler _adminCommandHandler;
        private readonly ILogger<MainHandler> _logger;

        public MainHandler(
            ITelegramBotClient botClient, RecoveryNotificationHandler recoveryNotificationHandler,
            ImageTextRecognitionHandler imageTextRecognitionHandler, ImageGeneratorHandler imageGeneratorHandler,
            CommandHandler commandHandler, ChatGptHandler chatGptHandler, AdminCommandHandler adminCommandHandler,
            ILogger<MainHandler> logger, UserService userService, UserCommandRepository userCommandRepository,
            MoneyService moneyService, ApplicationContext context, BotSettingsRepository botSettingsRepository,
            InvoiceRepository invoiceRepository, IGpt gptService)
        {
            _botClient = botClient;
            _recoveryNotificationHandler = recoveryNotificationHandler;
            _imageTextRecognitionHandler = imageTextRecognitionHandler;
            _imageGeneratorHandler = imageGeneratorHandler;
            _commandHandler = commandHandler;
            _chatGptHandler = chatGptHandler;
            _adminCommandHandler = adminCommandHandler;
            _logger = logger;
            _userService = userService;
            _userCommandRepository = userCommandRepository;
            _moneyService = moneyService;
            _context = context;
            _botSettingsRepository = botSettingsRepository;
            _invoiceRepository = invoiceRepository;
            _gptService = gptService;
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
                await _userService.CreateUpdateUser(newUser);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Couldn't create user with telegramId {userId} in database", newUser.Id);
            }

            var language = _botSettingsRepository.Get(userKey.Id)?.Language ?? update.Language;
            CultureInfo.CurrentUICulture = new CultureInfo(language);

            if (update.PreCheckoutQuery != null)
            {
                await _moneyService.AddMoneyAsync(update.UserChatKey.Id, update.PreCheckoutQuery.TotalAmount,
                    "TRX", CancellationToken.None);
                var profile = await _userService.GetUserProfile(update.UserChatKey.Id);
                var reply = String.Format(BotResponse.ProfileResponse, profile.FirstName,
                    profile.LastName, profile.Stars, profile.GptRequests, profile.Images, profile.ImageTexts);
                var replyMarkup = new InlineKeyboardMarkup(InlineKeyboardButton
                    .WithCallbackData(BotResponse.AddMoneyResponse, BotMenu.DepositCommand));

                await _botClient.AnswerPreCheckoutQueryAsync(
                    preCheckoutQueryId: update.PreCheckoutQuery.Id);
                await _botClient.SendTextMessageAsync(update.UserChatKey.Id, reply, replyMarkup: replyMarkup);
                return;
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
            else if (!string.IsNullOrEmpty(update.Message?.Text) && lastCommand?.Type == CommandType.Image)
            {
                SetNextHandler(_imageGeneratorHandler);
            }
            else if (!string.IsNullOrEmpty(update.FileId))
            {
                SetNextHandler(_imageTextRecognitionHandler);
            }
            else if (lastCommand?.Type == CommandType.Deposit)
            {
                if (!int.TryParse(update.Message?.Text, out var starsCount) && starsCount <= 0)
                {
                    throw new ClientException(update.UserChatKey.ChatId, BotResponse.InvalidDepositAmountResponse);
                }

                await _moneyService.SendInvoice(update.UserChatKey.Id, starsCount);

                return;
            }
            else if (lastCommand?.Type == CommandType.Music)
            {
                var isSuccessPayment = await _moneyService.PayForMusic(update.UserChatKey.Id);
                if (!isSuccessPayment)
                {
                    return;
                }

                var audio = await _gptService.GenerateMusicByText(update.Message!.Text, CancellationToken.None);
                await _botClient.SendAudioAsync(update.UserChatKey.Id, InputFile.FromStream(audio));
                return;
            }
            else
            {
                SetNextHandler(_chatGptHandler);
            }

            await base.HandleAsync(update);
        }
    }
}
