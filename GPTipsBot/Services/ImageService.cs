using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;
using static System.Net.Mime.MediaTypeNames;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.IO;

namespace GPTipsBot.Services
{
    public class ImageService
    {
        private readonly ITelegramBotClient _botClient;

        public ImageService(ITelegramBotClient botClient)
        {
            _botClient = botClient;
        }

        public Image<Rgba32> GetImage(string pathToImage)
        {
            using (var fileStream = new FileStream(pathToImage, FileMode.Open))
            {
                var image = Image.Load<Rgba32>(fileStream);
                return image;
            }
        }

        public byte[] ConvertImageToByteArray(Image<Rgba32> image)
        {
            using (var memoryStream = new MemoryStream())
            {
                image.Save(memoryStream, new JpegEncoder()); // Можно использовать другой тип сохранения
                return memoryStream.ToArray();
            }
        }

        public async Task SendImage(string chatId, string pathToImage)
        {
            var image = GetImage(pathToImage);

            using (var memoryStream = new MemoryStream())
            {
                image.Save(memoryStream, new JpegEncoder());
                var fileToSend = new InputMedia(memoryStream, "image");
                await _botClient.SendPhotoAsync(chatId, fileToSend, disableNotification: true, parseMode: ParseMode.Markdown);
            }
        }
    }
}
