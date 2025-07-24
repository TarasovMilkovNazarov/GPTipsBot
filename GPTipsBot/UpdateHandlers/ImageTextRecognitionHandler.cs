using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Extensions;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.Services.YandexCloud;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class ImageTextRecognitionHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<ImageTextRecognitionHandler> _logger;
        private readonly ITextRecognizer _yaCloudClient;
        private readonly MessageRepository _messageRepository;
        private readonly UserCommandRepository _userCommandRepository;
        private readonly IAdvertisementClient _advertisementClient;
        private readonly UserService _userService;
        private readonly ApplicationContext _context;
        private readonly TelejetAdClient _telejetAdClient;

        public ImageTextRecognitionHandler(ITelegramBotClient botClient, ILogger<ImageTextRecognitionHandler> logger,
            ITextRecognizer yaCloudClient, MessageRepository messageRepository, UserCommandRepository userCommandRepository,
            IAdvertisementClient gramadsAdvertisementClient, UserService userService, ApplicationContext context,
            TelejetAdClient telejetAdClient)
        {
            _botClient = botClient;
            _logger = logger;
            _yaCloudClient = yaCloudClient;
            _messageRepository = messageRepository;
            _userCommandRepository = userCommandRepository;
            _advertisementClient = gramadsAdvertisementClient;
            _userService = userService;
            _context = context;
            _telejetAdClient = telejetAdClient;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            string base64String;
            try
            {
                base64String = await update.GetPhotoAsync(_botClient);
            }
            catch (ArgumentNullException e)
            {
                await _botClient.SendTextMessageAsync(update.UserChatKey.ChatId,
                    BotResponse.SendTextRecognitionImage,
                    replyMarkup: TelegramBotUiService.CancelKeyboard);

                return;
            }

            var isAdmin = update.UserChatKey.IsAdmin();

            var lastCommand = await _userCommandRepository.GetLastAsync(update.UserChatKey);

            if (!isAdmin && lastCommand?.Type != CommandType.TextRecognition)
            {
                await _botClient.SendTextMessageAsync(update.UserChatKey.ChatId,
                    BotResponse.SendImageTextRecognitionCommandFirst,
                    replyMarkup: TelegramBotUiService.CancelKeyboard);

                return;
            }

            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            var isSuccessPayment = await _userService.PayForTextRecognitions(update.UserChatKey.ChatId);
            if (!isSuccessPayment)
            {
                await dbTransaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                await _botClient.SendTextMessageAsync(update.UserChatKey.ChatId, BotResponse.PleaseWaitMsg,
                    replyMarkup: TelegramBotUiService.DepositInlineKeyboard);
                return;
            }

            var text = await _yaCloudClient.Recognize(base64String);

            var recognitionResultMessage = new MessageDto(update.UserChatKey)
            {
                Text = text,
                BotMessageType = BotMessageType.RecognizeText,
                ContextBound = false,
                Role = MessageOwner.Ya,
            };

            await _messageRepository.AddAsync(recognitionResultMessage);
            await dbTransaction.CommitAsync();

            await _botClient.SendTextMessageAsync(update.UserChatKey.ChatId,
                text, replyToMessageId: (int)update.Message.TelegramMessageId!);

            await _advertisementClient.SendPostToChat(update.UserChatKey.ChatId);
            await _telejetAdClient.SendToBapAsync(update.TelegramUpdate, "activity");
        }
    }
}
