using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;

namespace GPTipsBot.Services
{
    public class ImageService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ImageCreatorService imageCreatorService;

        public ImageService(ITelegramBotClient botClient, ImageCreatorService imageCreatorService)
        {
            _botClient = botClient;
            this.imageCreatorService = imageCreatorService;
        }

        public async Task SendImage(long chatId, string fileName)
        {
            using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                var fileToSend = new InputMedia(fileStream, fileName);
                var message = await _botClient.SendPhotoAsync(chatId, fileToSend, disableNotification: true, parseMode: ParseMode.Markdown);
            }
        }
    }
}
