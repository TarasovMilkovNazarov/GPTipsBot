using GPTipsBot.Config;
using GPTipsBot.Localization;
using GPTipsBot.Resources;
using GPTipsBot.Services.Cache;
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

        public static ReplyMarkup? GetMenuMarkup(bool isGroupOrChannel) =>
            isGroupOrChannel ? null : StartKeyboard;

        public static ReplyMarkup GetCancelMarkup(bool isGroupOrChannel) =>
            isGroupOrChannel ? CancelInlineKeyboard : CancelKeyboard;

        public static ReplyMarkup GetChooseLangMarkup(bool isGroupOrChannel) =>
            isGroupOrChannel ? GetLanguageInlineKeyboard() : ChooseLangKeyboard;

        private static KeyboardButton ImagesMenuButton => new(BotUI.ImagesMenuButton);
        private static KeyboardButton AnimatePhotoButton => new(BotUI.AnimateButton);
        private static KeyboardButton PromptFromImageButton => new(BotUI.PromptFromImageButton);
        private static KeyboardButton ResetContextButton => new(BotUI.ResetContextButton);
        private static KeyboardButton HelpButton => new(BotUI.HelpButton);
        private static KeyboardButton CancelButton => new(BotUI.CancelButton);
        private static KeyboardButton RuLangButton => new(BotUI.RussianButton);
        private static KeyboardButton EngLangButton => new(BotUI.EnglishButton);
        private static KeyboardButton ProfileButton => new(BotUI.ProfileButton);
        private static KeyboardButton ModelButton => new(BotUI.ModelButton);

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
                { BotMenu.PromptFromImageCommand, new() },
                { BotMenu.DepositCommand, new() },
                { BotMenu.GetProfileCommand, new() },
                { BotMenu.AnimatePhotoCommand, new() },
                { BotMenu.ModelCommand, new() },
                { BotMenu.GptImageCommand, new() },
                { BotMenu.EditImageCommand, new() },
                { BotMenu.ImagesMenuCommand, new() },
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
                ButtonToLocalizations[BotMenu.PromptFromImageCommand].Add(BotUI.PromptFromImageButton);
                ButtonToLocalizations[BotMenu.DepositCommand].Add(BotUI.DepositButton);
                ButtonToLocalizations[BotMenu.GetProfileCommand].Add(BotUI.ProfileButton);
                ButtonToLocalizations[BotMenu.AnimatePhotoCommand].Add(BotUI.AnimateButton);
                ButtonToLocalizations[BotMenu.ModelCommand].Add(BotUI.ModelButton);
                ButtonToLocalizations[BotMenu.GptImageCommand].Add(BotUI.GptImageButton);
                ButtonToLocalizations[BotMenu.EditImageCommand].Add(BotUI.EditImageButton);
                ButtonToLocalizations[BotMenu.ImagesMenuCommand].Add(BotUI.ImagesMenuButton);
            }

            // Keep old reply-keyboard labels working until users get the new menu via /start.
            AddLegacyButtonLabels();

            CultureInfo.CurrentUICulture = savedCulture;
        }

        private static void AddLegacyButtonLabels()
        {
            AddUnique(BotMenu.ResetContextCommand,
                "💬 Новый диалог с ChatGPT (сбросить контекст)",
                "💬 New dialog with ChatGPT (reset context)");
            AddUnique(BotMenu.GetProfileCommand,
                "👤 Личный кабинет",
                "👤 Personal Account");
            AddUnique(BotMenu.DepositCommand,
                "Пополнить баланс",
                "Add funds");
            AddUnique(BotMenu.ImageTextRecognizeCommand,
                "Распознать текст на изображении",
                "Get text on image");
            AddUnique(BotMenu.HelpCommand, "❔ Help");
            AddUnique(BotMenu.ChooseLangCommand, "Язык", "Language");
            AddUnique(BotMenu.ImageCommand,
                "🖼 Создать изображение",
                "🖼 Create image",
                "🖼 Сreate image");
        }

        private static void AddUnique(string command, params string[] labels)
        {
            var list = ButtonToLocalizations[command];
            foreach (var label in labels)
            {
                if (!list.Exists(existing => string.Equals(existing, label, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add(label);
                }
            }
        }

        private static ReplyKeyboardMarkup GetMenuKeyboardMarkup()
        {
            var keyboardMarkup = new ReplyKeyboardMarkup(
            [
                [ResetContextButton, ProfileButton],
                [ModelButton, ImagesMenuButton],
                [PromptFromImageButton, AnimatePhotoButton],
                [HelpButton],
            ]);

            keyboardMarkup.ResizeKeyboard = true;
            keyboardMarkup.OneTimeKeyboard = false;

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

        private static InlineKeyboardMarkup GetLanguageInlineKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData(BotUI.RussianButton, BotMenu.SetRuLangCommand),
                InlineKeyboardButton.WithCallbackData(BotUI.EnglishButton, BotMenu.SetEngLangCommand),
            });
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

        public static InlineKeyboardMarkup GetModelSelectionKeyboard(string selectedModelId)
        {
            var rows = GptModelCatalog.All
                .Select(model => new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        model.FormatButtonLabel(string.Equals(model.Id, selectedModelId, StringComparison.OrdinalIgnoreCase)),
                        $"{BotMenu.ModelCommand} {model.Id}")
                })
                .Cast<IEnumerable<InlineKeyboardButton>>()
                .ToList();

            rows.Add([InlineKeyboardButton.WithCallbackData(BotUI.CancelButton, BotMenu.CancelCommand)]);

            return new InlineKeyboardMarkup(rows);
        }

        public static InlineKeyboardMarkup GetModelNeedsBalanceKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        GptModelCatalog.Default.FormatButtonLabel(false),
                        $"{BotMenu.ModelCommand} {GptModelCatalog.DefaultModelId}"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(BotResponse.AddMoneyResponse, BotMenu.DepositCommand),
                },
            });
        }

        public static InlineKeyboardMarkup GetProfileInlineKeyboard()
        {
            return new InlineKeyboardMarkup(
            [
                [InlineKeyboardButton.WithCallbackData(BotUI.ModelButton, BotMenu.ModelCommand)],
                [InlineKeyboardButton.WithCallbackData(BotUI.DepositButton, BotMenu.DepositCommand)],
                [InlineKeyboardButton.WithCallbackData(BotUI.LangButton, BotMenu.ChooseLangCommand)],
            ]);
        }

        public static InlineKeyboardMarkup GetImagesMenuInlineKeyboard()
        {
            return new InlineKeyboardMarkup(
            [
                [InlineKeyboardButton.WithCallbackData(BotUI.GptImageButton, BotMenu.GptImageCommand)],
                [InlineKeyboardButton.WithCallbackData(BotUI.EditImageButton, BotMenu.EditImageCommand)],
                [InlineKeyboardButton.WithCallbackData(BotUI.ImageButton, BotMenu.ImageCommand)],
                [InlineKeyboardButton.WithCallbackData(BotUI.ImageTextRecognizeButton, BotMenu.ImageTextRecognizeCommand)],
                [InlineKeyboardButton.WithCallbackData(BotUI.PromptFromImageButton, BotMenu.PromptFromImageCommand)],
            ]);
        }

        public static InlineKeyboardMarkup GetGptImageOptionsKeyboard(GptImageSession session)
        {
            var sizeRow = GptImageConfig.Sizes.Select(size =>
            {
                var selected = string.Equals(size.Id, session.Size, StringComparison.OrdinalIgnoreCase);
                var label = $"{(selected ? "✅ " : "")}{size.Label}";
                var command = size.Id switch
                {
                    GptImageConfig.SizeLandscape => BotMenu.GptImageSizeLandscapeCommand,
                    GptImageConfig.SizePortrait => BotMenu.GptImageSizePortraitCommand,
                    _ => BotMenu.GptImageSizeSquareCommand,
                };
                return InlineKeyboardButton.WithCallbackData(label, command);
            }).ToArray();

            var qualityRow = GptImageConfig.Qualities.Select(quality =>
            {
                var selected = string.Equals(quality.Id, session.Quality, StringComparison.OrdinalIgnoreCase);
                var label = $"{(selected ? "✅ " : "")}{quality.Label} · {quality.StarsCost:0.##}⭐";
                var command = quality.Id switch
                {
                    GptImageConfig.QualityLow => BotMenu.GptImageQualityLowCommand,
                    GptImageConfig.QualityHigh => BotMenu.GptImageQualityHighCommand,
                    _ => BotMenu.GptImageQualityMediumCommand,
                };
                return InlineKeyboardButton.WithCallbackData(label, command);
            }).ToArray();

            return new InlineKeyboardMarkup(
            [
                sizeRow,
                qualityRow,
                [InlineKeyboardButton.WithCallbackData(BotUI.CancelButton, BotMenu.CancelCommand)],
            ]);
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
