using GPTipsBot.Localization;
using GPTipsBot.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Globalization;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Services
{
    [JsonObject(MemberSerialization.OptIn, NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
    public class BotMenu
    {
        public const string StartCommand = "/start";
        public const string ImageCommand = "/image";
        public const string ResetContextCommand = "/reset_context";
        public const string FeedbackCommand = "/feedback";
        public const string HelpCommand = "/help";
        public const string ChooseLangCommand = "/setLang";
        public const string SetRuLangCommand = "/setRuLang";
        public const string SetEngLangCommand = "/setEngLang";
        public const string CancelCommand = "/cancel";
        public const string StopRequestCommand = "/stopRequest";
        public const string ImageTextRecognizeCommand = "/get_image_text";

        public static BotCommand Start => new() { Command = StartCommand, Description = BotUI.Start };
        public static BotCommand Image => new() { Command = ImageCommand, Description = BotUI.Image };
        public static BotCommand ImageRecText => new() { Command = ImageTextRecognizeCommand, Description = BotUI.ImageTextRecognize };
        public static BotCommand ResetContext => new() { Command = ResetContextCommand, Description = BotUI.ResetContext };
        public static BotCommand Feedback => new() { Command = FeedbackCommand, Description = BotUI.Feedback };
        public static BotCommand Help => new() { Command = HelpCommand, Description = BotUI.Help };
        public static BotCommand ChooseLang => new() { Command = ChooseLangCommand, Description = BotUI.SetLang };
        public static BotCommand SetRuLang => new() { Command = SetRuLangCommand, Description = BotUI.SetRuLang };
        public static BotCommand SetEngLang => new() { Command = SetEngLangCommand, Description = BotUI.SetEngLang };
        public static BotCommand StopRequest => new() { Command = StopRequestCommand };
        public static BotCommand Cancel => new() { Command = CancelCommand };

        public BotMenu()
        {
        }

        public BotCommand[] GetBotCommands()
        {
            return new[]
            {
                Start,
                Image,
                ImageRecText,
                ResetContext,
                Help,
            };
        }
    }

    public class TelegramBotUiService
    {
        public static ReplyKeyboardMarkup StartKeyboard => GetMenuKeyboardMarkup();
        public static ReplyKeyboardMarkup CancelKeyboard => GetCancelKeyboardMarkup();
        public static ReplyKeyboardMarkup ChooseLangKeyboard => GetLanguageKeyboardMarkup();

        private static KeyboardButton ImageButton => new(BotUI.ImageButton);
        private static KeyboardButton ImageRecognizeTextButton => new(BotUI.ImageTextRecognizeButton);
        private static KeyboardButton ResetContextButton => new(BotUI.ResetContextButton);
        private static KeyboardButton HelpButton => new(BotUI.HelpButton);
        private static KeyboardButton CancelButton => new(BotUI.CancelButton);
        private static KeyboardButton LangButton => new(BotUI.LangButton);
        private static KeyboardButton RuLangButton => new(BotUI.RussianButton);
        private static KeyboardButton EngLangButton => new(BotUI.EnglishButton);

        public static Dictionary<string, List<string>> ButtonToLocalizations { get; private set; }

        static TelegramBotUiService()
        {
            SetButtonToLocalizations();
        }

        // If user sends command from keyboard then it sends as text on choosen button
        private static void SetButtonToLocalizations()
        {
            ButtonToLocalizations = new Dictionary<string, List<string>>()
            {
                { BotMenu.ImageCommand, new() },
                { BotMenu.ResetContextCommand, new() },
                { BotMenu.HelpCommand, new() },
                { BotMenu.FeedbackCommand, new() },
                { BotMenu.CancelCommand, new() },
                { BotMenu.ChooseLangCommand, new() },
                { BotMenu.SetRuLangCommand, new() },
                { BotMenu.SetEngLangCommand, new() },
                { BotMenu.ImageTextRecognizeCommand, new() },
            };

            var savedCulture = CultureInfo.CurrentUICulture;

            foreach (var culture in LocalizationManager.SupportedCultures)
            {
                CultureInfo.CurrentUICulture = culture;

                ButtonToLocalizations[BotMenu.ImageCommand].Add(BotUI.ImageButton);
                ButtonToLocalizations[BotMenu.ResetContextCommand].Add(BotUI.ResetContextButton);
                ButtonToLocalizations[BotMenu.HelpCommand].Add(BotUI.HelpButton);
                ButtonToLocalizations[BotMenu.FeedbackCommand].Add(BotUI.FeedbackButton);
                ButtonToLocalizations[BotMenu.CancelCommand].Add(BotUI.CancelButton);
                ButtonToLocalizations[BotMenu.ChooseLangCommand].Add(BotUI.LangButton);
                ButtonToLocalizations[BotMenu.SetRuLangCommand].Add(BotUI.RussianButton);
                ButtonToLocalizations[BotMenu.SetEngLangCommand].Add(BotUI.EnglishButton);
                ButtonToLocalizations[BotMenu.ImageTextRecognizeCommand].Add(BotUI.ImageTextRecognizeButton);
            }

            CultureInfo.CurrentUICulture = savedCulture;
        }

        private static ReplyKeyboardMarkup GetMenuKeyboardMarkup()
        {
            var keyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new[]
                {
                    ResetContextButton,

                },
                new[]
                {
                    ImageButton,
                    ImageRecognizeTextButton
                },
                new[]
                {
                    LangButton,
                    HelpButton
                }
            });

            keyboardMarkup.ResizeKeyboard = true;
            keyboardMarkup.OneTimeKeyboard = true;

            return keyboardMarkup;
        }
        
        private static ReplyKeyboardMarkup GetCancelKeyboardMarkup()
        {
            var keyboardMarkup = new ReplyKeyboardMarkup(CancelButton);

            keyboardMarkup.ResizeKeyboard = true;
            keyboardMarkup.OneTimeKeyboard = true;

            return keyboardMarkup;
        }
        private static ReplyKeyboardMarkup GetLanguageKeyboardMarkup()
        {
            var keyboardMarkup = new ReplyKeyboardMarkup(new[] { RuLangButton, EngLangButton });

            keyboardMarkup.ResizeKeyboard = true;
            keyboardMarkup.OneTimeKeyboard = true;

            return keyboardMarkup;
        }
    }
}
