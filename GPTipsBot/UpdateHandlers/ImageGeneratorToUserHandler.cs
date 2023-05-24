using GPTipsBot.Api;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers
{
    public class ImageGeneratorToUserHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<ImageGeneratorToUserHandler> logger;
        private readonly ImageService imageService;
        private readonly ActionStatus sendImageStatus;
        public const int basedOnExperienceInputLengthLimit = 150;

        public ImageGeneratorToUserHandler(ITelegramBotClient botClient, ILogger<ImageGeneratorToUserHandler> logger,
            ImageService imageService, ActionStatus sendImagestatus)
        {
            this.botClient = botClient;
            this.logger = logger;
            this.imageService = imageService;
            this.sendImageStatus = sendImagestatus;
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var message = update.TelegramGptMessage;
            var userKey = update.TelegramGptMessage.UserKey;

            if (update.Update.Message.Text.Length > basedOnExperienceInputLengthLimit)
            {
                await botClient.SendTextMessageAsync(userKey.ChatId, BotResponse.ImageDescriptionLimitWarning, cancellationToken: cancellationToken, replyMarkup: new ReplyKeyboardMarkup(TelegramBotUIService.cancelButton));
                MainHandler.userState[userKey].CurrentState = Enums.UserStateEnum.None;
                return;
            }

            await sendImageStatus.Start(userKey, Telegram.Bot.Types.Enums.ChatAction.UploadPhoto, cancellationToken);
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                await imageService.SendImageToTelegramUser(userKey.ChatId, update.Update.Message.Text);
                sw.Stop();
                logger.LogInformation($"Successfull image generation for message {message.MessageId} takes {sw.Elapsed.TotalSeconds}s");
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
                MainHandler.userState[userKey].CurrentState = Enums.UserStateEnum.None;
                await sendImageStatus.Stop(userKey, cancellationToken);
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
