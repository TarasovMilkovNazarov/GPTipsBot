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
            var chatId = userKey.ChatId;

            if (update.Message.Text.Length > ImageTextDescriptionLimit)
            {
                await botClient.SendMessage(
                    chatId,
                    string.Format(BotResponse.ImageDescriptionLimitWarning, ImageTextDescriptionLimit),
                    replyMarkup: TelegramBotUiService.GetCancelMarkup(update.IsGroupOrChannel));
                return;
            }

            await using var dbTransaction = await context.Database.BeginTransactionAsync();
            var successPayment = await userService.PayForImageAsync(userKey.Id);
            if (!successPayment)
            {
                await dbTransaction.RollbackAsync();
                context.ChangeTracker.Clear();

                var nextExec = await jobService.GetNextExecutionForExistingJob<RefreshFreeLimitsJob>();
                await botClient.SendOutOfFreeRequestsMessageAsync(chatId, nextExec);
                return;
            }

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
            });

            await dbTransaction.CommitAsync();

            if (update.IsGroupOrChannel)
            {
                return;
            }

            await gramadsAdvertisementClient.SendPostToChat(chatId);
            await telejetAdClient.SendToBapAsync(update.TelegramUpdate, "activity");
            await advertisementTracker.TrySendAdvertisement(userKey.Id);
        }
    }
}
