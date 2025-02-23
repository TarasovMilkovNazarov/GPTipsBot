using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using GPTipsBot.Logging;
using GPTipsBot.Utilities;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using GPTipsBot.Exceptions;

namespace GPTipsBot.UpdateHandlers
{
    public class ImageTextRecognitionHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<ImageTextRecognitionHandler> logger;
        private readonly YandexTextRecognitionService yandexTextRecognitionService;

        public ImageTextRecognitionHandler(ITelegramBotClient botClient, ILogger<ImageTextRecognitionHandler> logger, YandexTextRecognitionService yandexTextRecognitionService)
        {
            this.botClient = botClient;
            this.logger = logger;
            this.yandexTextRecognitionService = yandexTextRecognitionService;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var file = await botClient.GetFileAsync(update.FileId);

            using var memoryStream = new MemoryStream();
            // Download the file into the MemoryStream
            await botClient.DownloadFileAsync(file.FilePath, memoryStream);

            // Reset the position of the MemoryStream to the beginning
            memoryStream.Position = 0;

            // Here you can process the image in memory as needed
            // For example, you can convert it to a byte array
            var base64String = Convert.ToBase64String(memoryStream.ToArray());
            var text = await yandexTextRecognitionService.Recognize(base64String);


            await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, text, replyToMessageId: (int)update.Message.TelegramMessageId!);
        }
    }
}
