using GPTipsBot.Api;
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

namespace GPTipsBot.UpdateHandlers
{
    public class ImageGeneratorHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<ImageGeneratorHandler> logger;
        private readonly ActionStatus sendImageStatus;
        private readonly ImageCreatorService imageCreatorService;
        private readonly MessageRepository messageRepository;
        public const int basedOnExperienceInputLengthLimit = 150;

        public ImageGeneratorHandler(ITelegramBotClient botClient, ILogger<ImageGeneratorHandler> logger,
            ActionStatus sendImagestatus, ImageCreatorService imageCreatorService, MessageRepository messageRepository)
        {
            this.botClient = botClient;
            this.logger = logger;
            this.sendImageStatus = sendImagestatus;
            this.imageCreatorService = imageCreatorService;
            this.messageRepository = messageRepository;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var userKey = update.UserChatKey;

            if (update.Message.Text.Length > basedOnExperienceInputLengthLimit)
            {
                await botClient.SendTextMessageAsync(userKey.ChatId, BotResponse.ImageDescriptionLimitWarning, replyMarkup: TelegramBotUIService.cancelKeyboard);
                MainHandler.userState[userKey].CurrentState = Enums.UserStateEnum.None;
                return;
            }

            update.ServiceMessage.TelegramMessageId = await sendImageStatus
                .Start(userKey, Telegram.Bot.Types.Enums.ChatAction.UploadPhoto);
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                var token = MainHandler.userState[update.UserChatKey]
                    .messageIdToCancellation[update.ServiceMessage.TelegramMessageId ?? 
                        throw new InvalidOperationException()].Token;

                var imgSrcs = await imageCreatorService.GenerateImage(update.Message.Text);
                update.Reply.Text = string.Join("\n", imgSrcs);
                messageRepository.AddMessage(update.Reply);
                var replyMarkup = TelegramBotUIService.cancelKeyboard;
                var telegramMediaList = imgSrcs.Select((src, i) => new InputMediaPhoto(InputFile.FromString(src))).ToList();

                var photoMessage = await botClient.SendMediaGroupAsync(userKey.ChatId, telegramMediaList, disableNotification: true,
                    replyToMessageId: (int?)update.Message.TelegramMessageId, cancellationToken: token);

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
                //MainHandler.userState[userKey].CurrentState = Enums.UserStateEnum.None;
                await sendImageStatus.Stop(userKey);
            }

            // Call next handler
            await base.HandleAsync(update);
        }
    }
}
