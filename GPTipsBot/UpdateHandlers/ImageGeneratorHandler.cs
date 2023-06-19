using GPTipsBot.Api;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
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

        public override async Task HandleAsync(UpdateDecorator update, CancellationToken cancellationToken)
        {
            var userKey = update.UserChatKey;

            if (update.Message.Text.Length > basedOnExperienceInputLengthLimit)
            {
                await botClient.SendTextMessageAsync(userKey.ChatId, BotResponse.ImageDescriptionLimitWarning, cancellationToken: cancellationToken, replyMarkup: TelegramBotUIService.cancelKeyboard);
                MainHandler.userState[userKey].CurrentState = Enums.UserStateEnum.None;
                return;
            }

            update.ServiceMessage.TelegramMessageId = await sendImageStatus.Start(userKey, Telegram.Bot.Types.Enums.ChatAction.UploadPhoto, cancellationToken);
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                var token = MainHandler.userState[update.UserChatKey]
                    .messageIdToCancellation[update.ServiceMessage.TelegramMessageId ?? throw new InvalidOperationException()].Token;

                var imgSrcs = await imageCreatorService.GetImageSources(update.Message.Text, token);
                update.Reply.Text = string.Join("\n",imgSrcs);
                messageRepository.AddMessage(update.Reply);
                var replyMarkup = TelegramBotUIService.cancelKeyboard;
                var telegramMediaList = imgSrcs.Select((src, i) => new InputMediaPhoto(InputFile.FromString(src))).ToList();

                var photoMessage = await botClient.SendMediaGroupAsync(userKey.ChatId, telegramMediaList, disableNotification: true, 
                    replyToMessageId: (int?)update.Message.TelegramMessageId, cancellationToken: token);

                sw.Stop();
                logger.LogInformation($"Successfull image generation for message {update.Message.Id} takes {sw.Elapsed.TotalSeconds}s");
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
