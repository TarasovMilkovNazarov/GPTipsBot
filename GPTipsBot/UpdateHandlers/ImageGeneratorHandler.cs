using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Logging;
using GPTipsBot.Utilities;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using GPTipsBot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.UpdateHandlers
{
    public class ImageGeneratorHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<ImageGeneratorHandler> _logger;
        private readonly IImageGenerator _ya;
        private readonly UserStatusActivator _sendImageStatus;
        private readonly ImageCreatorService _imageCreatorService;
        private readonly MessageRepository _messageRepository;
        private readonly GramadsAdvertisementClient _gramadsAdvertisementClient;
        public const int ImageTextDescriptionLimit = 1000;
        public const int ImagesPerDayLimit = 5;

        public ImageGeneratorHandler(ITelegramBotClient botClient, ILogger<ImageGeneratorHandler> logger, IImageGenerator ya,
            UserStatusActivator sendImagestatus, ImageCreatorService imageCreatorService,
            MessageRepository messageRepository, GramadsAdvertisementClient gramadsAdvertisementClient)
        {
            _botClient = botClient;
            _logger = logger;
            _ya = ya;
            _sendImageStatus = sendImagestatus;
            _imageCreatorService = imageCreatorService;
            _messageRepository = messageRepository;
            _gramadsAdvertisementClient = gramadsAdvertisementClient;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            // await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, "Sorry. This service temporary not available now", replyMarkup: TelegramBotUiService.CancelKeyboard);
            //
            // return;

            var userKey = update.UserChatKey;

            if (update.Message.Text.Length > ImageTextDescriptionLimit)
            {
                await _botClient.SendTextMessageAsync(userKey.ChatId,
                    String.Format(BotResponse.ImageDescriptionLimitWarning, ImageTextDescriptionLimit),
                    replyMarkup: TelegramBotUiService.CancelKeyboard);
                return;
            }

            if (_messageRepository.GetTodayImagesCount(userKey) >= ImagesPerDayLimit)
            {
                await _botClient.SendTextMessageAsync(userKey.ChatId,
                    String.Format(BotResponse.ImagesPerDayLimit, ImagesPerDayLimit),
                    replyMarkup: TelegramBotUiService.CancelKeyboard);
                return;
            }

            var serviceMessageId = await _sendImageStatus
                .Start(userKey, ChatAction.UploadPhoto);
            try
            {
                var sw = Stopwatch.StartNew();
                var token = MainHandler.UserState[update.UserChatKey]
                    .MessageIdToCancellation[serviceMessageId].Token;

                var response = await _ya.GenerateImage(update.Message.Text);
                var replyMarkup = TelegramBotUiService.CancelKeyboard;

                using var imageStream = new MemoryStream(Convert.FromBase64String(response));
                await _messageRepository.AddAsync(new MessageDto(update.UserChatKey)
                {
                    TelegramId = update.UserChatKey.Id,
                    Role = MessageOwner.Ya,
                    BotMessageType = BotMessageType.ImageGenerated,
                });
                await _botClient.SendPhotoAsync(userKey.ChatId, InputFile.FromStream(imageStream), cancellationToken: token);

                await _botClient.SendTextMessageAsync(userKey.ChatId, String.Format(BotResponse.InputImageDescriptionText,
                    ImageTextDescriptionLimit), replyMarkup: replyMarkup, disableNotification: true, cancellationToken: token);

                sw.Stop();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Image generation task was canceled");
            }
            catch (ClientException ex)
            {
                await _botClient.SendTextMessageAsync(userKey.ChatId, ex.Message, replyToMessageId: (int)update.Message.TelegramMessageId!);
            }
            catch (ImageCreatorException ex)
            {
                var statusCode = ex.Response?.StatusCode.ToString("G");

                _logger.WithProps(
                    () => _logger.LogError(ex, "Что-то пошло не так при получении ответа от создателя картинок."),
                    ("StatusCode", statusCode)
                    );
                
                await _botClient.SendTextMessageAsync(userKey.ChatId, BotResponse.SomethingWentWrongWithImageService);
            }
            catch(Exception ex)
            {
                await _botClient.SendTextMessageAsync(userKey.ChatId, BotResponse.SomethingWentWrongWithImageService);
            }
            finally
            {
                await _sendImageStatus.Stop(userKey);
            }

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
