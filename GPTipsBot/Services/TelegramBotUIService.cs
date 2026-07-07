using GPTipsBot.Localization;
using GPTipsBot.Resources;
using System.Globalization;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Services
{
    public class TelegramBotUiService
    {
        public static ReplyKeyboardMarkup StartKeyboard => GetMenuKeyboardMarkup();
        public static ReplyKeyboardMarkup CancelKeyboard => GetCancelKeyboardMarkup();
        public static ReplyKeyboardMarkup ChooseLangKeyboard => GetLanguageKeyboardMarkup();
        public static InlineKeyboardMarkup DepositInlineKeyboard => GetDepositInlineKeyboard();
        public static InlineKeyboardMarkup CancelInlineKeyboard => GetCancelInlineKeyboard();

        private static KeyboardButton ImageButton => new(BotUI.ImageButton);
        private static KeyboardButton AnimatePhotoButton => new(BotUI.AnimateButton);
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
                { BotMenu.AnimatePhotoCommand, new() },
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
                ButtonToLocalizations[BotMenu.AnimatePhotoCommand].Add(BotUI.AnimateButton);
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
                    ProfileButton
                },
                new[]
                {
                    ImageButton,
                    ImageRecognizeTextButton
                },
                new[]
                {
                    AnimatePhotoButton
                },
                new[]
                {
                    LangButton,
                    HelpButton
                },
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

        private static InlineKeyboardMarkup GetCancelInlineKeyboard()
        {
            return new InlineKeyboardMarkup(InlineKeyboardButton
                .WithCallbackData(BotUI.CancelButton, BotMenu.CancelCommand));
        }

        public static InlineKeyboardMarkup GetImageInstructionInlineKeyboard(bool isSquare)
        {
            return new InlineKeyboardMarkup
            {
                InlineKeyboard = new List<IEnumerable<InlineKeyboardButton>>()
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(isSquare ? "✅ 1:1 🟦" : "1:1 🟦",
                            BotMenu.ImageSquareCommand),
                        InlineKeyboardButton.WithCallbackData(isSquare ? "2:1 🟦🟦" : "✅ 2:1 🟦🟦",
                            BotMenu.ImageRectangleCommand),
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(BotUI.CancelButton, BotMenu.CancelCommand)
                    },
                }
            };
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
