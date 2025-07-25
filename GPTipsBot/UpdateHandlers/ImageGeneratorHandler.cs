using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using GPTipsBot.Jobs;
using GPTipsBot.Services.YandexCloud;
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
        private readonly IAdvertisementClient _advertisementClient;
        private readonly UserService _userService;
        private readonly ApplicationContext _context;
        private readonly TelejetAdClient _telejetAdClient;
        private readonly IJobService _jobService;
        public const int ImageTextDescriptionLimit = 1000;
        public const int ImagesPerDayLimit = 5;

        public ImageGeneratorHandler(ITelegramBotClient botClient, ILogger<ImageGeneratorHandler> logger,
            IImageGenerator ya, UserStatusActivator sendImagestatus, ImageCreatorService imageCreatorService,
            MessageRepository messageRepository, IAdvertisementClient gramadsAdvertisementClient,
            UserService userService, ApplicationContext context, TelejetAdClient telejetAdClient,
            IJobService jobService)
        {
            _botClient = botClient;
            _logger = logger;
            _ya = ya;
            _sendImageStatus = sendImagestatus;
            _imageCreatorService = imageCreatorService;
            _messageRepository = messageRepository;
            _advertisementClient = gramadsAdvertisementClient;
            _userService = userService;
            _context = context;
            _telejetAdClient = telejetAdClient;
            _jobService = jobService;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            // await botClient.SendMessage(update.UserChatKey.ChatId, "Sorry. This service temporary not available now", replyMarkup: TelegramBotUiService.CancelKeyboard);
            //
            // return;

            var userKey = update.UserChatKey;

            if (update.Message.Text.Length > ImageTextDescriptionLimit)
            {
                await _botClient.SendMessage(userKey.ChatId,
                    string.Format(BotResponse.ImageDescriptionLimitWarning, ImageTextDescriptionLimit),
                    replyMarkup: TelegramBotUiService.CancelKeyboard);
                return;
            }

            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            var successPayment = await _userService.PayForImageAsync(update.UserChatKey.Id);
            if (!successPayment)
            {
                await dbTransaction.RollbackAsync();
                _context.ChangeTracker.Clear();

                var nextExec = await _jobService.GetNextExecutionForExistingJob<RefreshFreeLimitsJob>();
                await _botClient.SendOutOfFreeRequestsMessageAsync(update.UserChatKey.Id, nextExec);
                return;
            }

            var serviceMessageId = await _sendImageStatus
                .Start(userKey, ChatAction.UploadPhoto);
            try
            {
                var sw = Stopwatch.StartNew();
                var token = Dispatcher.UserState[update.UserChatKey]
                    .MessageIdToCancellation[serviceMessageId].Token;
                var replyMarkup = TelegramBotUiService.CancelKeyboard;

                if (AppConfig.IsProduction)
                {
                    var response = await _ya.GenerateImage(update.Message.Text);

                    using var imageStream = new MemoryStream(Convert.FromBase64String(response));
                    await _messageRepository.AddAsync(new MessageDto(update.UserChatKey)
                    {
                        TelegramId = update.UserChatKey.Id,
                        Role = MessageOwner.Ya,
                        BotMessageType = BotMessageType.ImageGenerated,
                    });
                    await _botClient.SendPhoto(userKey.ChatId, InputFile.FromStream(imageStream), cancellationToken: token);
                }
                else
                {
                    await _messageRepository.AddAsync(new MessageDto(update.UserChatKey)
                    {
                        TelegramId = update.UserChatKey.Id,
                        Role = MessageOwner.Ya,
                        BotMessageType = BotMessageType.ImageGenerated,
                    });
                    await _botClient.SendPhoto(userKey.ChatId, InputFile
                        .FromUri("https://www.kasandbox.org/programming-images/avatars/leaf-blue.png"),
                        cancellationToken: token);
                }

                await _botClient.SendMessage(userKey.ChatId, string.Format(BotResponse.InputImageDescriptionText,
                    ImageTextDescriptionLimit), replyMarkup: replyMarkup, disableNotification: true, cancellationToken: token);

                sw.Stop();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Image generation task was canceled");
            }
            catch (ClientException ex)
            {
                await _botClient.SendMessage(userKey.ChatId, ex.Message, replyParameters: (int)update.Message.TelegramMessageId!);
            }
            catch (ImageCreatorException ex)
            {
                var statusCode = ex.Response?.StatusCode.ToString("G");

                _logger.WithProps(
                    () => _logger.LogError(ex, "Что-то пошло не так при получении ответа от создателя картинок."),
                    ("StatusCode", statusCode)
                    );
                
                await _botClient.SendMessage(userKey.ChatId, BotResponse.SomethingWentWrongWithImageService);
            }
            catch(Exception ex)
            {
                await _botClient.SendMessage(userKey.ChatId, BotResponse.SomethingWentWrongWithImageService);
            }
            finally
            {
                await _sendImageStatus.Stop(userKey);
            }

            await dbTransaction.CommitAsync();

            await _advertisementClient.SendPostToChat(update.UserChatKey.ChatId);
            await _telejetAdClient.SendToBapAsync(update.TelegramUpdate, "activity");
        }
    }
}
