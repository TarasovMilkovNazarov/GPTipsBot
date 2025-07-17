using GPTipsBot.Localization;
using GPTipsBot.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Globalization;
using GPTipsBot.Dtos;
using GPTipsBot.Models;
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
        public const string HelpCommand = "/help";
        public const string ChooseLangCommand = "/setLang";
        public const string SetRuLangCommand = "/setRuLang";
        public const string SetEngLangCommand = "/setEngLang";
        public const string CancelCommand = "/cancel";
        public const string StopRequestCommand = "/stopRequest";
        public const string ImageTextRecognizeCommand = "/get_image_text";
        public const string DepositCommand = "/deposit";
        public const string GetProfileCommand = "/profile";
        public const string DonateCommand = "/donate";

        public const string FixCommand = "/fix";
        public const string VersionCommand = "/version";

        public static CustomBotCommand Start => new() { Command = StartCommand, Description = BotUI.Start, Type = CommandType.Start};
        public static CustomBotCommand Image => new() { Command = ImageCommand, Description = BotUI.Image, Type = CommandType.Image };
        public static CustomBotCommand ImageRecText => new() { Command = ImageTextRecognizeCommand, Description = BotUI.ImageTextRecognize, Type = CommandType.TextRecognition};
        public static CustomBotCommand ResetContext => new() { Command = ResetContextCommand, Description = BotUI.ResetContext, Type = CommandType.ResetContext};
        public static CustomBotCommand Help => new() { Command = HelpCommand, Description = BotUI.Help, Type = CommandType.Help};
        public static CustomBotCommand ChooseLang => new() { Command = ChooseLangCommand, Description = BotUI.SetLang, Type = CommandType.ChooseLanguage};
        public static CustomBotCommand SetRuLang => new() { Command = SetRuLangCommand, Description = BotUI.SetRuLang, Type = CommandType.SetRuLang };
        public static CustomBotCommand SetEngLang => new() { Command = SetEngLangCommand, Description = BotUI.SetEngLang, Type = CommandType.SetEngLang };
        public static CustomBotCommand StopRequest => new() { Command = StopRequestCommand, Type = CommandType.StopRequest };
        public static CustomBotCommand Cancel => new() { Command = CancelCommand, Type = CommandType.CancelPreviousCommand };
        public static CustomBotCommand Deposit => new() { Command = DepositCommand, Description = BotUI.DepositButton, Type = CommandType.Deposit };
        public static CustomBotCommand Profile => new() { Command = GetProfileCommand, Description = BotUI.ProfileButton, Type = CommandType.GetProfile };
        public static CustomBotCommand Donate => new() { Command = DonateCommand, Description = BotUI.DonateButton, Type = CommandType.Donate };

        public static CustomBotCommand Fix => new() { Command = FixCommand, Type = CommandType.Admin };
        public static CustomBotCommand Version => new() { Command = VersionCommand, Type = CommandType.Admin };

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
                Deposit,
                Profile,
                Donate,
            };
        }
    }

    public class TelegramBotUiService
    {
        public static ReplyKeyboardMarkup StartKeyboard => GetMenuKeyboardMarkup();
        public static ReplyKeyboardMarkup CancelKeyboard => GetCancelKeyboardMarkup();
        public static ReplyKeyboardMarkup ChooseLangKeyboard => GetLanguageKeyboardMarkup();
        public static InlineKeyboardMarkup DepositInlineKeyboard => GetDepositInlineKeyboard();

        private static KeyboardButton ImageButton => new(BotUI.ImageButton);
        private static KeyboardButton ImageRecognizeTextButton => new(BotUI.ImageTextRecognizeButton);
        private static KeyboardButton ResetContextButton => new(BotUI.ResetContextButton);
        private static KeyboardButton HelpButton => new(BotUI.HelpButton);
        private static KeyboardButton CancelButton => new(BotUI.CancelButton);
        private static KeyboardButton LangButton => new(BotUI.LangButton);
        private static KeyboardButton RuLangButton => new(BotUI.RussianButton);
        private static KeyboardButton EngLangButton => new(BotUI.EnglishButton);
        private static KeyboardButton DepositButton => new(BotUI.DepositButton);
        private static KeyboardButton ProfileButton => new(BotUI.ProfileButton);

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
                { BotMenu.CancelCommand, new() },
                { BotMenu.ChooseLangCommand, new() },
                { BotMenu.SetRuLangCommand, new() },
                { BotMenu.SetEngLangCommand, new() },
                { BotMenu.ImageTextRecognizeCommand, new() },
                { BotMenu.DepositCommand, new() },
                { BotMenu.GetProfileCommand, new() },
            };

            var savedCulture = CultureInfo.CurrentUICulture;

            foreach (var culture in LocalizationManager.SupportedCultures)
            {
                CultureInfo.CurrentUICulture = culture;

                ButtonToLocalizations[BotMenu.ImageCommand].Add(BotUI.ImageButton);
                ButtonToLocalizations[BotMenu.ResetContextCommand].Add(BotUI.ResetContextButton);
                ButtonToLocalizations[BotMenu.HelpCommand].Add(BotUI.HelpButton);
                ButtonToLocalizations[BotMenu.CancelCommand].Add(BotUI.CancelButton);
                ButtonToLocalizations[BotMenu.ChooseLangCommand].Add(BotUI.LangButton);
                ButtonToLocalizations[BotMenu.SetRuLangCommand].Add(BotUI.RussianButton);
                ButtonToLocalizations[BotMenu.SetEngLangCommand].Add(BotUI.EnglishButton);
                ButtonToLocalizations[BotMenu.ImageTextRecognizeCommand].Add(BotUI.ImageTextRecognizeButton);
                ButtonToLocalizations[BotMenu.DepositCommand].Add(BotUI.DepositButton);
                ButtonToLocalizations[BotMenu.GetProfileCommand].Add(BotUI.ProfileButton);
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
                },
                new[]
                {
                    ProfileButton
                }
            });

            keyboardMarkup.ResizeKeyboard = true;
            keyboardMarkup.OneTimeKeyboard = true;

            return keyboardMarkup;
        }

        private static InlineKeyboardMarkup GetDepositInlineKeyboard()
        {
            return new InlineKeyboardMarkup(InlineKeyboardButton
                .WithCallbackData(BotResponse.AddMoneyResponse, BotMenu.DepositCommand));
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
