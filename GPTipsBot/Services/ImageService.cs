﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;
using System.IO;
using System.IO.Pipes;

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

        public async Task SendImageToTelegramUser(long chatId, string prompt)
        {
            byte[] image = imageCreatorService.GetImageFromText(prompt);
            MemoryStream stream = new MemoryStream(image);
            var fileToSend = new InputMedia(stream, "newFile");
            var message = await _botClient.SendPhotoAsync(chatId, fileToSend, disableNotification: true, parseMode: ParseMode.Markdown);
        }
    }
}
