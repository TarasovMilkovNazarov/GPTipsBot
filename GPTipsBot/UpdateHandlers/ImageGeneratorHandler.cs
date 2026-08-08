using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
using GPTipsBot.Jobs;
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
        ApplicationContext context,
        TelejetAdClient telejetAdClient,
        IJobService jobService,
        InMemoryAdvertisementTracker advertisementTracker,
        UserCommandRepository userCommandRepository,
        ImageGenerationWorkflowService imageGenerationWorkflowService)
        : BaseMessageHandler
    {
        public const int ImageTextDescriptionLimit = 500;
        public const int ImagesPerDayLimit = 5;

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var userKey = update.UserChatKey;

            if (update.Message.Text.Length > ImageTextDescriptionLimit)
            {
                await botClient.SendUserReplyAsync(
                    update,
                    string.Format(BotResponse.ImageDescriptionLimitWarning, ImageTextDescriptionLimit),
                    TelegramBotUiService.GetCancelMarkup(update.IsGroupOrChannel));
                return;
            }

            await using var dbTransaction = await context.Database.BeginTransactionAsync();
            var successPayment = await userService.PayForImageAsync(update.UserChatKey.Id);
            if (!successPayment)
            {
                await dbTransaction.RollbackAsync();
                context.ChangeTracker.Clear();

                var nextExec = await jobService.GetNextExecutionForExistingJob<RefreshFreeLimitsJob>();
                await botClient.SendOutOfFreeRequestsMessageAsync(update.ReplyChatId, nextExec);
                return;
            }

            var progressMessage = await botClient.SendMessage(update.ReplyChatId, BotResponse.PleaseWaitMsg);
            var previousCommand = await userCommandRepository.GetLastAsync(update.UserChatKey);
            var isSquare = previousCommand?.Type == CommandType.ImageSquare;

            await imageGenerationWorkflowService.StartAsync(new ImageGenerationWorkflowData
            {
                ChatId = userKey.ChatId,
                ReplyChatId = update.ReplyChatId,
                UserId = userKey.Id,
                Prompt = update.Message.Text,
                IsSquare = isSquare,
                ProgressMessageId = progressMessage.MessageId,
            });

            await dbTransaction.CommitAsync();

            if (update.IsGroupOrChannel)
            {
                await botClient.SendMessage(userKey.ChatId, BotResponse.ReplySentPrivately);
                return;
            }

            await gramadsAdvertisementClient.SendPostToChat(update.UserChatKey.ChatId);
            await telejetAdClient.SendToBapAsync(update.TelegramUpdate, "activity");
            await advertisementTracker.TrySendAdvertisement(update.UserChatKey.Id);
        }
    }
}
