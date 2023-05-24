using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Services
{
    [JsonObject(MemberSerialization.OptIn, NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
    public static class BotMenu
    {
        public static BotCommand Start { get; set; }
        public static BotCommand Image { get; set; }
        public static BotCommand ResetContext { get; set; }
        public static BotCommand Feedback { get; set; }
        public static BotCommand Help { get; set; }

        static BotMenu()
        {
            Start = new BotCommand { Command = "/start", Description = "Начать пользоваться ботом" };
            Image = new BotCommand { Command = "/image", Description = "Создать изображение по текстовому описанию" };
            ResetContext = new BotCommand { Command = "/reset_context", Description = "Сбросить контекст" };
            Feedback = new BotCommand { Command = "/feedback", Description = "Оставить отзыв" };
            Help = new BotCommand { Command = "/help", Description = "Инструкция по применению" };
        }

        public static BotCommand[] GetBotCommands()
        {
            return new BotCommand[]
            {
                Start,
                Image,
                ResetContext,
                Feedback,
                Help
            };
        }
    }

    public class TelegramBotUIService
    {
        private readonly ITelegramBotClient botClient;
        public static ReplyKeyboardMarkup startKeyboard;
        public static KeyboardButton imageButton;
        public static KeyboardButton resetContextButton;
        public static KeyboardButton feedbackButton;
        public static KeyboardButton helpButton;
        public static KeyboardButton cancelButton;

        public TelegramBotUIService(ITelegramBotClient botClient)
        {
            this.botClient = botClient;
        }

        static TelegramBotUIService()
        {
            imageButton = new KeyboardButton("🖼 Создать изображение") { };
            resetContextButton = new KeyboardButton("🗑 Сбросить контекст");
            helpButton = new KeyboardButton("❔ Help");
            feedbackButton = new KeyboardButton("Оставить отзыв");
            cancelButton = new KeyboardButton("Отмена");
            startKeyboard = GetMenuKeyboardMarkup();
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
                    helpButton,
                    feedbackButton
                }
            });

            keyboardMarkup.ResizeKeyboard = true;
            keyboardMarkup.OneTimeKeyboard = true;

            return keyboardMarkup;
        }
    }
}
