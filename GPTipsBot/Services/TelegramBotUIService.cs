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
        public const string StartStr = "/start";
        public const string ImageStr = "/image";
        public const string ResetContextStr = "/reset_context";
        public const string FeedbackStr = "/feedback";
        public const string HelpStr = "/help";
        public const string ChooseLangStr = "/setLang";
        public const string SetRuLangStr = "/setRuLang";
        public const string SetEngLangStr = "/setEngLang";
        public const string GamesStr = "/games";
        public const string CancelStr = "/cancel";
        public const string StopRequestStr = "/stopRequest";
        public const string TickTackToeStr = "/tickTackToe";
        public const string EmojiTranslationStr = "/emojiTranslation";
        public const string BookDivinationStr = "/bookDivination";
        public const string GuessWhoStr = "/guessWho";
        public const string AdventureStr = "/adventureGame";

        public static BotCommand Start => new BotCommand { Command = StartStr, Description = BotUI.Start };
        public static BotCommand Image => new BotCommand { Command = ImageStr, Description = BotUI.Image };
        public static BotCommand ResetContext => new BotCommand { Command = ResetContextStr, Description = BotUI.ResetContext };
        public static BotCommand Feedback => new BotCommand { Command = FeedbackStr, Description = BotUI.Feedback };
        public static BotCommand Help => new BotCommand { Command = HelpStr, Description = BotUI.Help };
        public static BotCommand ChooseLang => new BotCommand { Command = ChooseLangStr, Description = BotUI.SetLang };
        public static BotCommand SetRuLang => new BotCommand { Command = SetRuLangStr, Description = BotUI.SetRuLang };
        public static BotCommand SetEngLang => new BotCommand { Command = SetEngLangStr, Description = BotUI.SetEngLang };
        public static BotCommand StopRequest => new BotCommand { Command = StopRequestStr };

        #region Games commands
        public static BotCommand Games => new BotCommand { Command = GamesStr, Description = BotUI.GamesButton };
        public static BotCommand TickTackToe => new BotCommand { Command = TickTackToeStr, Description = BotUI.TickTackToeButton };
        public static BotCommand EmojiTranslation => new BotCommand { Command = EmojiTranslationStr, Description = BotUI.EmojiTranslationButton };
        public static BotCommand BookDivination => new BotCommand { Command = BookDivinationStr, Description = BotUI.BookDivinationButton };
        public static BotCommand GuessWho => new BotCommand { Command = GuessWhoStr, Description = BotUI.GuessWhoButton };
        public static BotCommand Adventure => new BotCommand { Command = AdventureStr, Description = BotUI.AdventureButton };
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
                { BotMenu.ImageStr, new() },
                { BotMenu.ResetContextStr, new() },
                { BotMenu.HelpStr, new() },
                { BotMenu.FeedbackStr, new() },
                { BotMenu.CancelStr, new() },
                { BotMenu.ChooseLangStr, new() },
                { BotMenu.SetRuLangStr, new() },
                { BotMenu.SetEngLangStr, new() },
                { BotMenu.GamesStr, new() },
                { BotMenu.TickTackToeStr, new() },
                { BotMenu.BookDivinationStr, new() },
                { BotMenu.EmojiTranslationStr, new() },
                { BotMenu.GuessWhoStr, new() },
                { BotMenu.AdventureStr, new() },
            };

            var savedCulture = CultureInfo.CurrentUICulture;

            foreach (var culture in LocalizationManager.SupportedCultures)
            {
                CultureInfo.CurrentUICulture = culture;

                ButtonToLocalizations[BotMenu.ImageStr].Add(BotUI.ImageButton);
                ButtonToLocalizations[BotMenu.ResetContextStr].Add(BotUI.ResetContextButton);
                ButtonToLocalizations[BotMenu.HelpStr].Add(BotUI.HelpButton);
                ButtonToLocalizations[BotMenu.FeedbackStr].Add(BotUI.FeedbackButton);
                ButtonToLocalizations[BotMenu.CancelStr].Add(BotUI.CancelButton);
                ButtonToLocalizations[BotMenu.ChooseLangStr].Add(BotUI.LangButton);
                ButtonToLocalizations[BotMenu.SetRuLangStr].Add(BotUI.RussianButton);
                ButtonToLocalizations[BotMenu.SetEngLangStr].Add(BotUI.EnglishButton);
                ButtonToLocalizations[BotMenu.GamesStr].Add(BotUI.GamesButton);
                ButtonToLocalizations[BotMenu.TickTackToeStr].Add(BotUI.TickTackToeButton);
                ButtonToLocalizations[BotMenu.BookDivinationStr].Add(BotUI.BookDivinationButton);
                ButtonToLocalizations[BotMenu.EmojiTranslationStr].Add(BotUI.EmojiTranslationButton);
                ButtonToLocalizations[BotMenu.GuessWhoStr].Add(BotUI.GuessWhoButton);
                ButtonToLocalizations[BotMenu.AdventureStr].Add(BotUI.AdventureButton);
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
