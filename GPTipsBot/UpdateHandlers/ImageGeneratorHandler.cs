using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
using GPTipsBot.Jobs;
using GPTipsBot.Localization;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.Services.YandexCloud.Workflow;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class ImageGeneratorHandler(
        ITelegramBotClient botClient,
        IAdvertisementClient gramadsAdvertisementClient,
        UserService userService,
        TelejetAdClient telejetAdClient,
        IJobService jobService,
        InMemoryAdvertisementTracker advertisementTracker,
        UserCommandRepository userCommandRepository,
        ImageGenerationWorkflowService imageGenerationWorkflowService)
        : BaseMessageHandler
    {
        public const int ImageTextDescriptionLimit = 500;

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var userKey = update.UserChatKey;
            var chatId = userKey.ChatId;

            if (update.Message.Text.Length > ImageTextDescriptionLimit)
            {
                await botClient.SendMessageWithMenuAsync(
                    chatId,
                    string.Format(BotResponse.ImageDescriptionLimitWarning, ImageTextDescriptionLimit),
                    TelegramBotUiService.BackToImagesMenuInlineKeyboard,
                    update.IsGroupOrChannel);
                return;
            }

            var hold = await userService.TryReserveImageAsync(userKey.Id);
            if (hold is null)
            {
                var nextExec = await jobService.GetNextExecutionForExistingJob<RefreshFreeLimitsJob>();
                await botClient.SendOutOfFreeRequestsMessageAsync(chatId, nextExec);
                return;
            }

            try
            {
                var progressMessage = await botClient.SendMessage(chatId, BotResponse.PleaseWaitMsg);
                var previousCommand = await userCommandRepository.GetLastAsync(update.UserChatKey);
                var isSquare = previousCommand?.Type == CommandType.ImageSquare;

                await imageGenerationWorkflowService.StartAsync(new ImageGenerationWorkflowData
                {
                    ChatId = chatId,
                    ReplyChatId = chatId,
                    UserId = userKey.Id,
                    Prompt = update.Message.Text,
                    IsSquare = isSquare,
                    ProgressMessageId = progressMessage.MessageId,
                    PaymentHoldId = hold.Id,
                    UiLanguage = LocalizationManager.CurrentLanguage(),
                });
            }
            catch
            {
                await userService.ReleaseAsync(hold.Id);
                throw;
            }

            if (update.IsGroupOrChannel)
            {
                return;
            }

            await gramadsAdvertisementClient.SendPostToChat(chatId);
            await telejetAdClient.SendToBapAsync(update.TelegramUpdate, "activity");
            await advertisementTracker.TrySendAdvertisement(update.TelegramUserId);
        }
    }
}
