using GPTipsBot.Localization;
using GPTipsBot.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Globalization;
using Telegram.Bot;
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
        public const string GamesCommand = "/games";
        public const string CancelCommand = "/cancel";
        public const string StopRequestCommand = "/stopRequest";
        public const string TickTackToeCommand = "/tickTackToe";
        public const string EmojiTranslationCommand = "/emojiTranslation";
        public const string BookDivinationCommand = "/bookDivination";
        public const string GuessWhoCommand = "/guessWho";
        public const string AdventureCommand = "/adventureGame";

        public static BotCommand Start => new BotCommand { Command = StartCommand, Description = BotUI.Start };
        public static BotCommand Image => new BotCommand { Command = ImageCommand, Description = BotUI.Image };
        public static BotCommand ResetContext => new BotCommand { Command = ResetContextCommand, Description = BotUI.ResetContext };
        public static BotCommand Feedback => new BotCommand { Command = FeedbackCommand, Description = BotUI.Feedback };
        public static BotCommand Help => new BotCommand { Command = HelpCommand, Description = BotUI.Help };
        public static BotCommand ChooseLang => new BotCommand { Command = ChooseLangCommand, Description = BotUI.SetLang };
        public static BotCommand SetRuLang => new BotCommand { Command = SetRuLangCommand, Description = BotUI.SetRuLang };
        public static BotCommand SetEngLang => new BotCommand { Command = SetEngLangCommand, Description = BotUI.SetEngLang };
        public static BotCommand StopRequest => new BotCommand { Command = StopRequestCommand };
        public static BotCommand Cancel => new BotCommand { Command = CancelCommand };

        #region Games commands
        public static BotCommand Games => new BotCommand { Command = GamesCommand, Description = BotUI.GamesButton };
        public static BotCommand TickTackToe => new BotCommand { Command = TickTackToeCommand, Description = BotUI.TickTackToeButton };
        public static BotCommand EmojiTranslation => new BotCommand { Command = EmojiTranslationCommand, Description = BotUI.EmojiTranslationButton };
        public static BotCommand BookDivination => new BotCommand { Command = BookDivinationCommand, Description = BotUI.BookDivinationButton };
        public static BotCommand GuessWho => new BotCommand { Command = GuessWhoCommand, Description = BotUI.GuessWhoButton };
        public static BotCommand Adventure => new BotCommand { Command = AdventureCommand, Description = BotUI.AdventureButton };
        #endregion

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
                Games,
                Feedback,
                Help
            };
        }
    }

    public class TelegramBotUIService
    {
        public static ReplyKeyboardMarkup startKeyboard => GetMenuKeyboardMarkup();
        public static ReplyKeyboardMarkup cancelKeyboard => GetCancelKeyboardMarkup();
        public static ReplyKeyboardMarkup chooseLangKeyboard => GetLanguageKeyboardMarkup();
        public static ReplyKeyboardMarkup gamesKeyboard => GetGamesKeyboardMarkup();

        public static KeyboardButton imageButton => new KeyboardButton(BotUI.ImageButton);
        public static KeyboardButton resetContextButton => new KeyboardButton(BotUI.ResetContextButton);
        public static KeyboardButton feedbackButton => new KeyboardButton(BotUI.FeedbackButton);
        public static KeyboardButton helpButton => new KeyboardButton(BotUI.HelpButton);
        public static KeyboardButton cancelButton => new KeyboardButton(BotUI.CancelButton);
        public static KeyboardButton langButton => new KeyboardButton(BotUI.LangButton);
        public static KeyboardButton ruLangButton => new KeyboardButton(BotUI.RussianButton);
        public static KeyboardButton engLangButton => new KeyboardButton(BotUI.EnglishButton);

        public static KeyboardButton gamesButton => new KeyboardButton(BotUI.GamesButton);
        public static KeyboardButton tickTackToeButton => new KeyboardButton(BotUI.TickTackToeButton);
        public static KeyboardButton emojiTranslationButton => new KeyboardButton(BotUI.EmojiTranslationButton);
        public static KeyboardButton guessWhoButton => new KeyboardButton(BotUI.GuessWhoButton);
        public static KeyboardButton bookDivinationButton => new KeyboardButton(BotUI.BookDivinationButton);
        public static KeyboardButton adventureGameButton => new KeyboardButton(BotUI.AdventureButton);

        public static Dictionary<string, List<string>> ButtonToLocalizations { get; private set; }

        public TelegramBotUIService()
        {
        }

        static TelegramBotUIService()
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
                { BotMenu.GamesCommand, new() },
                { BotMenu.TickTackToeCommand, new() },
                { BotMenu.BookDivinationCommand, new() },
                { BotMenu.EmojiTranslationCommand, new() },
                { BotMenu.GuessWhoCommand, new() },
                { BotMenu.AdventureCommand, new() },
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
                ButtonToLocalizations[BotMenu.GamesCommand].Add(BotUI.GamesButton);
                ButtonToLocalizations[BotMenu.TickTackToeCommand].Add(BotUI.TickTackToeButton);
                ButtonToLocalizations[BotMenu.BookDivinationCommand].Add(BotUI.BookDivinationButton);
                ButtonToLocalizations[BotMenu.EmojiTranslationCommand].Add(BotUI.EmojiTranslationButton);
                ButtonToLocalizations[BotMenu.GuessWhoCommand].Add(BotUI.GuessWhoButton);
                ButtonToLocalizations[BotMenu.AdventureCommand].Add(BotUI.AdventureButton);
            }

            CultureInfo.CurrentUICulture = savedCulture;
        }

        private static ReplyKeyboardMarkup GetMenuKeyboardMarkup()
        {
            var keyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new[]
                {
                    resetContextButton,
                    imageButton
                },
                new[]
                {
                    gamesButton
                },
                new[]
                {
                    helpButton,
                    feedbackButton
                },
                new[]
                {
                    langButton
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
        private static ReplyKeyboardMarkup GetGamesKeyboardMarkup()
        {
            var keyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new[]
                {
                    tickTackToeButton,
                    emojiTranslationButton
                },
                new[]
                {
                    guessWhoButton,
                    bookDivinationButton
                },
                new[]
                {
                    adventureGameButton
                }
            });

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
