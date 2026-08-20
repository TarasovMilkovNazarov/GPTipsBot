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
    public class ImageTextRecognitionHandler(
        ITelegramBotClient botClient,
        ILogger<ImageTextRecognitionHandler> logger,
        ITextRecognizer yaCloudClient,
        MessageRepository messageRepository,
        UserCommandRepository userCommandRepository,
        IAdvertisementClient gramadsAdvertisementClient,
        UserService userService,
        TelejetAdClient telejetAdClient)
        : BaseMessageHandler
    {
        public override async Task HandleAsync(UpdateDecorator update)
        {
            string base64String;
            try
            {
                base64String = await update.GetPhotoAsync(botClient);
            }
            catch (ArgumentNullException e)
            {
                await botClient.SendUserReplyAsync(
                    update,
                    BotResponse.SendTextRecognitionImage,
                    TelegramBotUiService.BackToImagesMenuInlineKeyboard);

                return;
            }

            var isAdmin = update.UserChatKey.IsAdmin();

            var lastCommand = await userCommandRepository.GetLastAsync(update.UserChatKey);

            if (!isAdmin && lastCommand?.Type != CommandType.TextRecognition)
            {
                await botClient.SendUserReplyAsync(
                    update,
                    BotResponse.SendImageTextRecognitionCommandFirst,
                    TelegramBotUiService.BackToImagesMenuInlineKeyboard);

                return;
            }

            var hold = await userService.TryReserveTextRecognitionAsync(update.UserChatKey.Id);
            if (hold is null)
            {
                await botClient.SendUserReplyAsync(update, BotResponse.PleaseWaitMsg,
                    TelegramBotUiService.DepositInlineKeyboard);
                return;
            }

            var confirmed = false;
            try
            {
                var text = await yaCloudClient.Recognize(base64String);

                var recognitionResultMessage = new MessageDto(update.UserChatKey)
                {
                    Text = text,
                    BotMessageType = BotMessageType.RecognizeText,
                    ContextBound = false,
                    Role = MessageOwner.Ya,
                };

                await messageRepository.AddAsync(recognitionResultMessage);

                await botClient.SendUserReplyAsync(update, text);
                await userService.ConfirmAsync(hold.Id);
                confirmed = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Text recognition failed for user {UserId}", update.UserChatKey.Id);
                await botClient.SendUserReplyAsync(update, BotResponse.SomethingWentWrong);
            }
            finally
            {
                if (!confirmed)
                {
                    await userService.ReleaseAsync(hold.Id);
                }
            }

            if (!confirmed || update.IsGroupOrChannel)
            {
                return;
            }

            await gramadsAdvertisementClient.SendPostToChat(update.UserChatKey.ChatId);
            await telejetAdClient.SendToBapAsync(update.TelegramUpdate, "activity");
        }
    }
}
