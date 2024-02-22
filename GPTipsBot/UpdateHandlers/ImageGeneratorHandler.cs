using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using GPTipsBot.Logging;
using GPTipsBot.Utilities;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using GPTipsBot.Exceptions;
using GPTipsBot.Enums;

namespace GPTipsBot.UpdateHandlers
{
    public class ImageGeneratorHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<ImageGeneratorHandler> logger;
        private readonly ActionStatus sendImageStatus;
        private readonly ImageCreatorService imageCreatorService;
        private readonly MessageRepository messageRepository;
        private readonly HandlerFactory messageHandlerFactory;
        public const int imageTextDescriptionLimit = 1000;
        public const int imagesPerDayLimit = 10;

        public ImageGeneratorHandler(ITelegramBotClient botClient, ILogger<ImageGeneratorHandler> logger,
            ActionStatus sendImagestatus, ImageCreatorService imageCreatorService, MessageRepository messageRepository,
            HandlerFactory messageHandlerFactor)
        {
            this.botClient = botClient;
            this.logger = logger;
            this.sendImageStatus = sendImagestatus;
            this.imageCreatorService = imageCreatorService;
            this.messageRepository = messageRepository;
            this.messageHandlerFactory = messageHandlerFactor;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var isProhibitedRequest = await HandleViolations(update);
            if (isProhibitedRequest)
            {
                return;
            }

            var userKey = update.UserChatKey;
            update.ServiceMessage.TelegramMessageId = await sendImageStatus
                .Start(userKey, Telegram.Bot.Types.Enums.ChatAction.UploadPhoto);
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                var token = MainHandler.userState[userKey]
                    .messageIdToCancellation[update.ServiceMessage.TelegramMessageId ?? 
                        throw new InvalidOperationException()].Token;

                var imgSrcs = await imageCreatorService.GenerateImage(update.Message.Text);
                update.Reply.Text = string.Join("\n", imgSrcs);
                messageRepository.AddMessage(update.Reply);
                var replyMarkup = CommandService.CancelKeyboard;
                var telegramMediaList = imgSrcs.Select((src, i) => new InputMediaPhoto(InputFile.FromString(src))).ToList();

                await botClient.SendMediaGroupAsync(userKey.ChatId, telegramMediaList, disableNotification: true,
                    replyToMessageId: (int?)update.Message.TelegramMessageId, cancellationToken: token);

                await botClient.SendTextMessageAsync(userKey.ChatId, String.Format(BotResponse.InputImageDescriptionText, 
                    imageTextDescriptionLimit), replyMarkup: replyMarkup, disableNotification: true, cancellationToken: token);

                sw.Stop();
                logger.LogInformation(
                    $"Successful image generation for request {update.Message.Text.Truncate(30)} takes {sw.Elapsed.TotalSeconds}s");
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Image generation task was canceled");
            }
            catch (ClientException ex)
            {
                await botClient.SendTextMessageAsync(userKey.ChatId, ex.Message, replyToMessageId: (int)update.Message.TelegramMessageId!);
            }
            catch (ImageCreatorException ex)
            {
                var statusCode = ex.Response?.StatusCode.ToString("G");
                var contentBase64 = StringUtilities.Base64Encode(ex.Response?.Content);
                var headersBase64 = ex.Response?.Headers is null ? null : StringUtilities.Base64Encode(JsonConvert.SerializeObject(ex.Response?.Headers));
                
                logger.WithProps(
                    () => logger.LogError(ex, "Что-то пошло не так при получении ответа от создателя картинок."),
                    ("StatusCode", statusCode) // , ("ContentBase64", contentBase64), ("ResponseHeadersBase64", headersBase64) - очень большие получаются в логи не влазят
                    );
                
                await botClient.SendTextMessageAsync(userKey.ChatId, BotResponse.SomethingWentWrongWithImageService);
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Couldn't get images from bing");
                await botClient.SendTextMessageAsync(userKey.ChatId, BotResponse.SomethingWentWrongWithImageService);
            }
            finally
            {
                await sendImageStatus.Stop(userKey);
            }

            // Call next handler
            await base.HandleAsync(update);
        }

        private async Task<bool> HandleViolations(UpdateDecorator update)
        {
            var userKey = update.UserChatKey;

            if (update.Message.Text.Length > imageTextDescriptionLimit)
            {
                await botClient.SendTextMessageAsync(userKey.ChatId, String.Format(BotResponse.ImageDescriptionLimitWarning, imageTextDescriptionLimit), replyMarkup: CommandService.CancelKeyboard);
                MainHandler.userState[userKey].CurrentState = Enums.UserStateEnum.None;

                return true;
            }

            if (messageRepository.GetTodayImagesCount(userKey) > imagesPerDayLimit)
            {
                await botClient.SendTextMessageAsync(userKey.ChatId, String.Format(BotResponse.ImagesPerDayLimit, imagesPerDayLimit), replyMarkup: CommandService.CancelKeyboard);
                MainHandler.userState[userKey].CurrentState = Enums.UserStateEnum.None;
                return true;
            }

            return false;
        }
    }
}
