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
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<ImageGeneratorHandler> logger;
        private readonly IImageGenerator ya;
        private readonly ActionStatus sendImageStatus;
        private readonly ImageCreatorService imageCreatorService;
        private readonly MessageRepository messageRepository;
        public const int ImageTextDescriptionLimit = 1000;
        public const int ImagesPerDayLimit = 5;

        public ImageGeneratorHandler(ITelegramBotClient botClient, ILogger<ImageGeneratorHandler> logger, IImageGenerator ya,
            ActionStatus sendImagestatus, ImageCreatorService imageCreatorService, MessageRepository messageRepository)
        {
            this.botClient = botClient;
            this.logger = logger;
            this.ya = ya;
            this.sendImageStatus = sendImagestatus;
            this.imageCreatorService = imageCreatorService;
            this.messageRepository = messageRepository;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            // await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, "Sorry. This service temporary not available now", replyMarkup: TelegramBotUiService.CancelKeyboard);
            //
            // return;

            var userKey = update.UserChatKey;

            if (update.Message.Text.Length > ImageTextDescriptionLimit)
            {
                await botClient.SendTextMessageAsync(userKey.ChatId, String.Format(BotResponse.ImageDescriptionLimitWarning, ImageTextDescriptionLimit), replyMarkup: TelegramBotUiService.CancelKeyboard);
                MainHandler.userState[userKey].CurrentState = Enums.UserStateEnum.None;
                return;
            }

            if (messageRepository.GetTodayImagesCount(userKey) >= ImagesPerDayLimit)
            {
                await botClient.SendTextMessageAsync(userKey.ChatId,
                    String.Format(BotResponse.ImagesPerDayLimit, ImagesPerDayLimit),
                    replyMarkup: TelegramBotUiService.CancelKeyboard);
                MainHandler.userState[userKey].CurrentState = Enums.UserStateEnum.None;
                return;
            }

            update.ServiceMessage.TelegramMessageId = await sendImageStatus
                .Start(userKey, Telegram.Bot.Types.Enums.ChatAction.UploadPhoto);
            try
            {
                var sw = Stopwatch.StartNew();
                var token = MainHandler.userState[update.UserChatKey]
                    .messageIdToCancellation[update.ServiceMessage.TelegramMessageId ?? 
                        throw new InvalidOperationException()].Token;

                var response = await ya.GenerateImage(update.Message.Text);
                var replyMarkup = TelegramBotUiService.CancelKeyboard;

                using var imageStream = new MemoryStream(Convert.FromBase64String(response));
                messageRepository.AddMessage(new MessageDto
                {
                    TelegramId = update.UserChatKey.Id,
                    ChatId = update.UserChatKey.ChatId,
                    Role = MessageOwner.Ya,
                    UserId = update.UserChatKey.Id,
                    BotMessageType = BotMessageType.ImageGenerated,
                    ContextBound = false,
                    NewContext = false
                });
                await botClient.SendPhotoAsync(userKey.ChatId, InputFile.FromStream(imageStream), cancellationToken: token);

                await botClient.SendTextMessageAsync(userKey.ChatId, String.Format(BotResponse.InputImageDescriptionText,
                    ImageTextDescriptionLimit), replyMarkup: replyMarkup, disableNotification: true, cancellationToken: token);

                sw.Stop();
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

                logger.WithProps(
                    () => logger.LogError(ex, "Что-то пошло не так при получении ответа от создателя картинок."),
                    ("StatusCode", statusCode)
                    );
                
                await botClient.SendTextMessageAsync(userKey.ChatId, BotResponse.SomethingWentWrongWithImageService);
            }
            catch(Exception ex)
            {
                await botClient.SendTextMessageAsync(userKey.ChatId, BotResponse.SomethingWentWrongWithImageService);
            }
            finally
            {
                await sendImageStatus.Stop(userKey);
            }

            // Call next handler
            await base.HandleAsync(update);
        }
    }
}
