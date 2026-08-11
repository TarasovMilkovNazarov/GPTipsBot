using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Extensions;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class PromptFromImageHandler(
        ITelegramBotClient botClient,
        ILogger<PromptFromImageHandler> logger,
        IGpt gptService,
        MessageRepository messageRepository,
        UserCommandRepository userCommandRepository,
        IAdvertisementClient gramadsAdvertisementClient,
        UserService userService,
        ApplicationContext context,
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
            catch (ArgumentNullException)
            {
                await botClient.SendUserReplyAsync(
                    update,
                    BotResponse.SendPromptFromImagePhoto,
                    TelegramBotUiService.GetCancelMarkup(update.IsGroupOrChannel));
                return;
            }

            var isAdmin = update.UserChatKey.IsAdmin();
            var lastCommand = await userCommandRepository.GetLastAsync(update.UserChatKey);

            if (!isAdmin && lastCommand?.Type != CommandType.PromptFromImage)
            {
                await botClient.SendUserReplyAsync(
                    update,
                    BotResponse.SendPromptFromImageCommandFirst,
                    TelegramBotUiService.GetCancelMarkup(update.IsGroupOrChannel));
                return;
            }

            var hold = await userService.TryReserveGptAsync(update.UserChatKey.Id, GptModelCatalog.Default);
            if (hold is null)
            {
                await botClient.SendUserReplyAsync(
                    update,
                    BotResponse.SimpleNoFreeRequests,
                    TelegramBotUiService.DepositInlineKeyboard);
                return;
            }

            var confirmed = false;
            var progress = await botClient.SendMessage(
                update.UserChatKey.ChatId,
                BotResponse.PleaseWaitMsg,
                messageThreadId: update.Message?.MessageThreadId is long tid ? (int)tid : null);

            try
            {
                await using var dbTransaction = await context.Database.BeginTransactionAsync();

                var imageBytes = Convert.FromBase64String(base64String);
                var response = await gptService.SendVisionOneOffAsync(
                    BotResponse.PromptFromImageSystemPrompt,
                    BotResponse.PromptFromImageUserPrompt,
                    imageBytes,
                    "jpeg",
                    CancellationToken.None,
                    GptModelCatalog.DefaultModelId);

                var promptText = response.Choices.FirstOrDefault()?.Message.Content;
                if (string.IsNullOrWhiteSpace(promptText))
                {
                    await botClient.SendUserReplyAsync(update, BotResponse.SomethingWentWrong);
                    return;
                }

                var resultMessage = new MessageDto(update.UserChatKey)
                {
                    Text = promptText,
                    BotMessageType = BotMessageType.PromptFromImage,
                    ContextBound = false,
                    Role = MessageOwner.Assistant,
                };

                await messageRepository.AddAsync(resultMessage);
                await dbTransaction.CommitAsync();

                await botClient.SendUserReplyAsync(
                    update,
                    $"{BotResponse.PromptFromImageHeader}\n\n{promptText}");
                await userService.ConfirmAsync(hold.Id);
                confirmed = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Prompt from image failed for user {UserId}", update.UserChatKey.Id);
                await botClient.SendUserReplyAsync(update, BotResponse.SomethingWentWrong);
            }
            finally
            {
                if (!confirmed)
                {
                    await userService.ReleaseAsync(hold.Id);
                }

                try
                {
                    await botClient.DeleteMessage(update.UserChatKey.ChatId, progress.MessageId);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to delete progress message {MessageId}", progress.MessageId);
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
