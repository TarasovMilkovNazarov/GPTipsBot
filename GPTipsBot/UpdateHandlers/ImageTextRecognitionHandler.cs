using Ardalis.GuardClauses;
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
        private readonly GramadsAdvertisementClient _gramadsAdvertisementClient;
        private readonly UserService _userService;
        public const int ImagesPerDayLimit = 5;

        public ImageTextRecognitionHandler(ITelegramBotClient botClient, ILogger<ImageTextRecognitionHandler> logger,
            ITextRecognizer yaCloudClient, MessageRepository messageRepository, UserCommandRepository userCommandRepository,
            GramadsAdvertisementClient gramadsAdvertisementClient, UserService userService)
        {
            _botClient = botClient;
            _logger = logger;
            _yaCloudClient = yaCloudClient;
            _messageRepository = messageRepository;
            _userCommandRepository = userCommandRepository;
            _gramadsAdvertisementClient = gramadsAdvertisementClient;
            _userService = userService;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            // await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, "Sorry. This service temporary not available now", replyMarkup: TelegramBotUiService.CancelKeyboard);
            //
            // return;
            var isAdmin = update.UserChatKey.IsAdmin();

            var lastCommand = await _userCommandRepository.GetLastAsync(update.UserChatKey);

            if (!isAdmin && lastCommand?.Type != CommandType.TextRecognition)
            {
                await _botClient.SendTextMessageAsync(update.UserChatKey.ChatId,
                    BotResponse.SendImageTextRecognitionCommandFirst,
                    replyMarkup: TelegramBotUiService.CancelKeyboard);

                return;
            }

            if (_messageRepository.GetTodayTextRecognitionCount(update.UserChatKey) > ImagesPerDayLimit)
            {
                await _botClient.SendTextMessageAsync(update.UserChatKey.ChatId,
                    string.Format(BotResponse.ImagesPerDayLimit, ImagesPerDayLimit),
                    replyMarkup: TelegramBotUiService.CancelKeyboard);
                return;
            }

            var file = await _botClient.GetFileAsync(update.FileId);

            Guard.Against.Null(file.FilePath);

            using var memoryStream = new MemoryStream();
            await _botClient.DownloadFileAsync(file.FilePath, memoryStream);
            memoryStream.Position = 0;

            var base64String = Convert.ToBase64String(memoryStream.ToArray());
            var text = await _yaCloudClient.Recognize(base64String);

            var recognitionResultMessage = new MessageDto(update.UserChatKey)
            {
                Text = text,
                BotMessageType = BotMessageType.RecognizeText,
                ContextBound = false,
                Role = MessageOwner.Ya,
            };

            await _messageRepository.AddAsync(recognitionResultMessage);

            await _botClient.SendTextMessageAsync(update.UserChatKey.ChatId,
                text, replyToMessageId: (int)update.Message.TelegramMessageId!);

            await _userService.DecreaseFreeImageRecognitionsAsync(update.UserChatKey.ChatId);

            try
            {
                await _gramadsAdvertisementClient.SendPostToChat(update.UserChatKey.ChatId);
            }
            catch (Exception e)
            {
                // ignore
            }
        }
    }
}
