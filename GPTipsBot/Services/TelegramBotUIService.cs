using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Services
{
    public class TelegramBotUIService
    {
        private readonly ITelegramBotClient botClient;
        public static ReplyKeyboardMarkup startKeyboard;
        public static KeyboardButton imageButton;
        public static KeyboardButton resetContextButton;
        public static KeyboardButton helpButton;

        public TelegramBotUIService(ITelegramBotClient botClient)
        {
            this.botClient = botClient;
        }

        static TelegramBotUIService()
        {
            startKeyboard = GetMenuKeyboardMarkup();
            imageButton = new KeyboardButton("🖼 Создать изображение");
            resetContextButton = new KeyboardButton("🗑 Сбросить контекст");
            helpButton = new KeyboardButton("❔ Help");
        }

        private static ReplyKeyboardMarkup GetMenuKeyboardMarkup()
        {
            var keyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new[]
                {
                    imageButton,
                    resetContextButton
                },
                new[]
                {
                    helpButton
                }
            });

            keyboardMarkup.ResizeKeyboard = true;
            keyboardMarkup.OneTimeKeyboard = true;

            return keyboardMarkup;
        }
    }
}
