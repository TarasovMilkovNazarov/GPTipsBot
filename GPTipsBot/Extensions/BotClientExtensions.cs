using GPTipsBot.Services;
using GPTipsBot.Utilities;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.Extensions
{
    public static class BotClientExtensions
    {
        public static async Task SendTextMessageWithMenuKeyboard(this ITelegramBotClient botClient, long chatId, string text)
        {
            await botClient.SendTextMessageAsync(chatId, text, replyMarkup: TelegramBotUIService.startKeyboard);
        }

        public static async Task SendMarkdown2MessageAsync(
            this ITelegramBotClient botClient, 
            long chatId, 
            string text, 
            int? replyToMessageId = null,
            int partsLimit = -1
            )
        {
            var textParts = SplitIfTooLong(text);
            var partsCount = partsLimit == -1 || partsLimit > textParts.Count ? textParts.Count : partsLimit;

            foreach (var part in textParts.Take(partsCount))
            {
                var escapedText = StringUtilities.EscapeTextForMarkdown2(part)!;
                await botClient.SendTextMessageAsync(chatId, escapedText, null, ParseMode.MarkdownV2, replyToMessageId: replyToMessageId);
            }
        }

        private static List<string> SplitIfTooLong(string input)
        {
            var parts = new List<string>();
            const int maxLength = 4096 - 7 - 7;
            for (var i = 0; i < input.Length; i += maxLength)
            {
                var length = Math.Min(maxLength, input.Length - i);
                parts.Add(input.Substring(i, length));
            }

            var fixedParts = new List<string>();
            var previousWasClosed = false;

            foreach (var part in parts)
            {
                var text = part;

                if (previousWasClosed)
                    text = "```" + Environment.NewLine + text;

                if (HasUnclosedCodeBlock(text))
                {
                    fixedParts.Add(@$"{text}{Environment.NewLine}```");
                    previousWasClosed = true;
                }
                else
                {
                    fixedParts.Add(text);
                    previousWasClosed = false;
                }
            }

            return fixedParts;
        }

        private static bool HasUnclosedCodeBlock(string text) => (text.Split(new[] { "```" }, StringSplitOptions.None).Length - 1) % 2 != 0;
    }
}