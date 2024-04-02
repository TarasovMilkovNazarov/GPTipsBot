using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Microsoft.SemanticKernel.Text;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot
{
    public class UpdateHandlerEntryPoint
    {
        private readonly MainHandler mainHandler;
        private readonly ITelegramBotClient telegramBotClient;
        private readonly SpeechToTextService speechToTextService;
        private readonly TelejetAdClient telejetAdClient;

        public static DateTime Start { get; private set; }

        public UpdateHandlerEntryPoint(MainHandler mainHandler, ITelegramBotClient telegramBotClient,
            SpeechToTextService speechToTextService, TelejetAdClient telejetAdClient)
        {
            this.mainHandler = mainHandler;
            this.telegramBotClient = telegramBotClient;
            this.speechToTextService = speechToTextService;
            this.telejetAdClient = telejetAdClient;
        }

        static UpdateHandlerEntryPoint()
        {
            Start = DateTime.UtcNow;
        }

        public async Task HandleUpdateAsync(Update update)
        {
            string filePath = "John Grey - Men Are from Mars Women Are from Venus.txt";
            var text = "";

            // Check if the file exists
            if (System.IO.File.Exists(filePath))
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        text += line;
                        Console.WriteLine(line);
                    }
                }
            }
            else
            {
                Console.WriteLine("File not found.");
            }

            update.Message.Text = text;

            var needHandleUpd = await telejetAdClient.HandleUpdateAsync(update);
            if (!needHandleUpd)
            {
                return;
            }

            if (update.Ignore())
                return;

            var extendedUpd = new UpdateDecorator(update);
            if (update.Message?.Voice != null)
            {
                extendedUpd.Message.Text = await speechToTextService.RecognizeVoice(update.Message.Voice.FileId);
            }

            CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(extendedUpd.Language);
            await mainHandler.HandleAsync(extendedUpd);
        }
    }
}