using GPTipsBot.Localization;
using GPTipsBot.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Services
{
    [JsonObject(MemberSerialization.OptIn, NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
    public class BotMenu
    {
        public static BotCommand Start => new BotCommand { Command = "/start", Description = BotUI.Start };
        public static BotCommand Image => new BotCommand { Command = "/image", Description = BotUI.Image };
        public static BotCommand ResetContext => new BotCommand { Command = "/reset_context", Description = BotUI.ResetContext };
        public static BotCommand Feedback => new BotCommand { Command = "/feedback", Description = BotUI.Feedback };
        public static BotCommand Help => new BotCommand { Command = "/help", Description = BotUI.Help };
        public static BotCommand SetLang => new BotCommand { Command = "/setLang", Description = BotUI.SetLang };

        public BotMenu()
        {
        }

        public BotCommand[] GetBotCommands()
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
        public static ReplyKeyboardMarkup cancelKeyboard;
        public static ReplyKeyboardMarkup chooseLangKeyboard;

        public static KeyboardButton imageButton;
        public static KeyboardButton resetContextButton;
        public static KeyboardButton feedbackButton;
        public static KeyboardButton helpButton;
        public static KeyboardButton cancelButton;
        public static KeyboardButton langButton;
        public static KeyboardButton ruLangButton;
        public static KeyboardButton engLangButton;

        public static Dictionary<KeyboardButton, List<string>> ButtonToLocalizations { get; private set; }

        public TelegramBotUIService(ITelegramBotClient botClient)
        {
            this.botClient = botClient;
        }

        static TelegramBotUIService()
        {
            imageButton = new KeyboardButton(BotUI.ImageButton) { };
            resetContextButton = new KeyboardButton(BotUI.ResetContext);
            helpButton = new KeyboardButton(BotUI.HelpButton);
            feedbackButton = new KeyboardButton(BotUI.FeedbackButton);
            cancelButton = new KeyboardButton(BotUI.CancelButton);
            langButton = new KeyboardButton(BotUI.LangButton);
            ruLangButton = new KeyboardButton(BotUI.RussianButton);
            engLangButton = new KeyboardButton(BotUI.EnglishButton);
            startKeyboard = GetMenuKeyboardMarkup();
            cancelKeyboard = GetCancelKeyboardMarkup();
            chooseLangKeyboard = GetLanguageKeyboardMarkup();

            SetButtonToLocalizations();
        }

        private static void SetButtonToLocalizations()
        {
            ButtonToLocalizations = new Dictionary<KeyboardButton, List<string>>()
            {
                { imageButton, new() },
                { resetContextButton, new() },
                { helpButton, new() },
                { feedbackButton, new() },
                { cancelButton, new() },
                { langButton, new() },
                { ruLangButton, new() },
                { engLangButton, new() },
            };

            foreach (var culture in LocalizationManager.SupportedCultures)
            {
                CultureInfo.CurrentUICulture = culture;

                ButtonToLocalizations[imageButton].Add(BotUI.ImageButton);
                ButtonToLocalizations[resetContextButton].Add(BotUI.ResetContextButton);
                ButtonToLocalizations[helpButton].Add(BotUI.HelpButton);
                ButtonToLocalizations[feedbackButton].Add(BotUI.FeedbackButton);
                ButtonToLocalizations[cancelButton].Add(BotUI.CancelButton);
                ButtonToLocalizations[langButton].Add(BotUI.LangButton);
                ButtonToLocalizations[ruLangButton].Add(BotUI.RussianButton);
                ButtonToLocalizations[engLangButton].Add(BotUI.EnglishButton);
            }
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

        private static ReplyKeyboardMarkup GetCancelKeyboardMarkup()
        {
            var keyboardMarkup = new ReplyKeyboardMarkup(cancelButton);

            keyboardMarkup.ResizeKeyboard = true;
            keyboardMarkup.OneTimeKeyboard = true;

            return keyboardMarkup;
        }
        private static ReplyKeyboardMarkup GetLanguageKeyboardMarkup()
        {
            var keyboardMarkup = new ReplyKeyboardMarkup(new[] { ruLangButton, engLangButton });

            keyboardMarkup.ResizeKeyboard = true;
            keyboardMarkup.OneTimeKeyboard = true;

            return keyboardMarkup;
        }
    }
}
