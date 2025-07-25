using GPTipsBot.Db;
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
        ApplicationContext context,
        TelejetAdClient telejetAdClient)
        : BaseMessageHandler
    {
        private readonly ILogger<ImageTextRecognitionHandler> _logger = logger;

        public override async Task HandleAsync(UpdateDecorator update)
        {
            string base64String;
            try
            {
                base64String = await update.GetPhotoAsync(botClient);
            }
            catch (ArgumentNullException e)
            {
                await botClient.SendMessage(update.UserChatKey.ChatId,
                    BotResponse.SendTextRecognitionImage,
                    replyMarkup: TelegramBotUiService.CancelKeyboard);

                return;
            }

            var isAdmin = update.UserChatKey.IsAdmin();

            var lastCommand = await userCommandRepository.GetLastAsync(update.UserChatKey);

            if (!isAdmin && lastCommand?.Type != CommandType.TextRecognition)
            {
                await botClient.SendMessage(update.UserChatKey.ChatId,
                    BotResponse.SendImageTextRecognitionCommandFirst,
                    replyMarkup: TelegramBotUiService.CancelKeyboard);

                return;
            }

            await using var dbTransaction = await context.Database.BeginTransactionAsync();
            var isSuccessPayment = await userService.PayForTextRecognitions(update.UserChatKey.ChatId);
            if (!isSuccessPayment)
            {
                await dbTransaction.RollbackAsync();
                context.ChangeTracker.Clear();
                await botClient.SendMessage(update.UserChatKey.ChatId, BotResponse.PleaseWaitMsg,
                    replyMarkup: TelegramBotUiService.DepositInlineKeyboard);
                return;
            }

            var text = await yaCloudClient.Recognize(base64String);

            var recognitionResultMessage = new MessageDto(update.UserChatKey)
            {
                Text = text,
                BotMessageType = BotMessageType.RecognizeText,
                ContextBound = false,
                Role = MessageOwner.Ya,
            };

            await messageRepository.AddAsync(recognitionResultMessage);
            await dbTransaction.CommitAsync();

            await botClient.SendMessage(update.UserChatKey.ChatId,
                text, replyParameters: (int)update.Message.TelegramMessageId!);

            await gramadsAdvertisementClient.SendPostToChat(update.UserChatKey.ChatId);
            await telejetAdClient.SendToBapAsync(update.TelegramUpdate, "activity");
        }
    }
}
