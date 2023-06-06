using GPTipsBot.Api;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static System.Net.Mime.MediaTypeNames;

namespace GPTipsBot.UpdateHandlers
{
    public class ImageGeneratorHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<ImageGeneratorHandler> logger;
        private readonly ImageService imageService;
        private readonly ActionStatus sendImageStatus;
        private readonly ImageCreatorService imageCreatorService;
        private readonly MessageRepository messageRepository;
        public const int basedOnExperienceInputLengthLimit = 150;

        public ImageGeneratorHandler(ITelegramBotClient botClient, ILogger<ImageGeneratorHandler> logger,
            ImageService imageService, ActionStatus sendImagestatus, ImageCreatorService imageCreatorService, MessageRepository messageRepository)
        {
            this.botClient = botClient;
            this.logger = logger;
            this.imageService = imageService;
            this.sendImageStatus = sendImagestatus;
            this.imageCreatorService = imageCreatorService;
            this.messageRepository = messageRepository;
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var message = update.TelegramGptMessage;
            var userKey = update.TelegramGptMessage.UserKey;

            if (update.Update.Message.Text.Length > basedOnExperienceInputLengthLimit)
            {
                await botClient.SendTextMessageAsync(userKey.ChatId, BotResponse.ImageDescriptionLimitWarning, cancellationToken: cancellationToken, replyMarkup: TelegramBotUIService.cancelKeyboard);
                MainHandler.userState[userKey].CurrentState = Enums.UserStateEnum.None;
                return;
            }

            message.ServiceMessageId = await sendImageStatus.Start(userKey, Telegram.Bot.Types.Enums.ChatAction.UploadPhoto, cancellationToken);
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                var token = MainHandler.userState[message.UserKey].messageIdToCancellation[message.ServiceMessageId].Token;

                var imgSrcs = await imageCreatorService.GetImageSources(update.Update.Message.Text, token);
                message.Reply = string.Join("\n",imgSrcs);
                messageRepository.AddBingImageCreatorResponse(message);
                var replyMarkup = TelegramBotUIService.cancelKeyboard;
                var telegramMediaList = imgSrcs.Select((img, i) => new InputMediaPhoto(new InputMedia(img))).ToList();

                var photoMessage = await botClient.SendMediaGroupAsync(userKey.ChatId, telegramMediaList, disableNotification: true, 
                    replyToMessageId: (int)message.TelegramMessageId, cancellationToken: token);

                sw.Stop();
                logger.LogInformation($"Successfull image generation for message {message.MessageId} takes {sw.Elapsed.TotalSeconds}s");
            }
            catch(OperationCanceledException ex)
            {
                logger.LogInformation("Image generation task was canceled");
            }
            catch(CustomException ex)
            {
                await botClient.SendTextMessageAsync(userKey.ChatId, ex.Message, cancellationToken: cancellationToken);
            }
            catch(Exception ex)
            {
                await botClient.SendTextMessageAsync(userKey.ChatId, BotResponse.SomethingWentWrongWithImageService, cancellationToken: cancellationToken);
            }
            finally
            {
                //MainHandler.userState[userKey].CurrentState = Enums.UserStateEnum.None;
                await sendImageStatus.Stop(userKey, cancellationToken);
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
