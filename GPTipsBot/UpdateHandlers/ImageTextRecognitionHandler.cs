using GPTipsBot.Enums;
using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
namespace GPTipsBot.UpdateHandlers
{
    public class ImageTextRecognitionHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<ImageTextRecognitionHandler> logger;
        private readonly ITextRecognizer yaCloudClient;
        private readonly MessageRepository messageRepository;
        public const int ImagesPerDayLimit = 5;

        public ImageTextRecognitionHandler(ITelegramBotClient botClient, ILogger<ImageTextRecognitionHandler> logger,
            ITextRecognizer yaCloudClient, MessageRepository messageRepository)
        {
            this.botClient = botClient;
            this.logger = logger;
            this.yaCloudClient = yaCloudClient;
            this.messageRepository = messageRepository;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            // await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, "Sorry. This service temporary not available now", replyMarkup: TelegramBotUiService.CancelKeyboard);
            //
            // return;
            var value = MainHandler.userState.GetValueOrDefault(update.UserChatKey)?.CurrentState;
            var isAdmin = update.UserChatKey.IsAdmin();

            if (!isAdmin && value != UserStateEnum.AwaitingTextRecognitionImage)
            {
                await botClient.SendTextMessageAsync(update.UserChatKey.ChatId,
                    BotResponse.SendImageTextRecognitionCommandFirst,
                    replyMarkup: TelegramBotUiService.CancelKeyboard);

                return;
            }

            if (messageRepository.GetTodayTextRecognitionCount(update.UserChatKey) > ImagesPerDayLimit)
            {
                await botClient.SendTextMessageAsync(update.UserChatKey.ChatId,
                    String.Format(BotResponse.ImagesPerDayLimit, ImagesPerDayLimit),
                    replyMarkup: TelegramBotUiService.CancelKeyboard);
                MainHandler.userState[update.UserChatKey].CurrentState = Enums.UserStateEnum.None;
                return;
            }

            var file = await botClient.GetFileAsync(update.FileId);

            using var memoryStream = new MemoryStream();
            await botClient.DownloadFileAsync(file.FilePath, memoryStream);
            memoryStream.Position = 0;

            var base64String = Convert.ToBase64String(memoryStream.ToArray());
            var text = await yaCloudClient.Recognize(base64String);
            update.Reply.Text = text;
            update.Reply.BotMessageType = BotMessageType.RecognizeText;
            update.Reply.ContextBound = false;
            update.Reply.Role = MessageOwner.Ya;

            messageRepository.AddMessage(update.Reply);

            await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, text, replyToMessageId: (int)update.Message.TelegramMessageId!);
        }
    }
}
