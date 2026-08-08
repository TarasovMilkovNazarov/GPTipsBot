using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.Utilities;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Extensions
{
    public static class BotClientExtensions
    {
        public static async Task SendTextMessageWithMenuKeyboard(this ITelegramBotClient botClient, long chatId, string text)
        {
            await botClient.SendMessage(chatId, text, replyMarkup: TelegramBotUiService.StartKeyboard);
        }
        
        public static async Task SendBotVersionAsync(this ITelegramBotClient botClient, params long[] chatIds)
        {
            foreach (var chatId in chatIds)
            {
                await botClient.SendMessage(chatId, $"""
Bot running on:

Version: {StringUtilities.EscapeTextForMarkdown2(AppConfig.Version)}
CommitHash: [{AppConfig.CommitHash}](https://github.com/TarasovMilkovNazarov/GPTipsBot/commit/{AppConfig.CommitHash})
""", ParseMode.MarkdownV2);
            }
        }

        /// <summary>
        /// Sends a user-facing reply to the private chat when the update came from a group.
        /// On 403 (user never opened DM), falls back to the group with instructions.
        /// </summary>
        public static async Task<bool> SendUserReplyAsync(
            this ITelegramBotClient botClient,
            UpdateDecorator update,
            string text,
            ReplyMarkup? replyMarkup = null,
            bool acknowledgeInGroup = true)
        {
            try
            {
                await botClient.SendMessage(update.ReplyChatId, text, replyMarkup: replyMarkup);
                await AcknowledgePrivateReplyAsync(botClient, update, acknowledgeInGroup);
                return true;
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 403 && update.IsGroupOrChannel)
            {
                await botClient.SendMessage(
                    update.UserChatKey.ChatId,
                    string.Format(BotResponse.OpenPrivateChatFirst, AppConfig.BotName.TrimStart('@')),
                    replyParameters: ToReplyParameters(update));
                return false;
            }
        }

        public static async Task<bool> TrySendUserMarkdownReplyAsync(
            this ITelegramBotClient botClient,
            UpdateDecorator update,
            string text,
            ILogger? logger = null,
            bool acknowledgeInGroup = true)
        {
            try
            {
                await botClient.TrySendMarkdown2MessageAsync(update.ReplyChatId, text, replyToMessageId: null, logger: logger);
                await AcknowledgePrivateReplyAsync(botClient, update, acknowledgeInGroup);
                return true;
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 403 && update.IsGroupOrChannel)
            {
                await botClient.SendMessage(
                    update.UserChatKey.ChatId,
                    string.Format(BotResponse.OpenPrivateChatFirst, AppConfig.BotName.TrimStart('@')),
                    replyParameters: ToReplyParameters(update));
                return false;
            }
        }

        private static async Task AcknowledgePrivateReplyAsync(
            ITelegramBotClient botClient,
            UpdateDecorator update,
            bool acknowledgeInGroup)
        {
            if (!acknowledgeInGroup || !update.IsGroupOrChannel)
            {
                return;
            }

            try
            {
                await botClient.SendMessage(
                    update.UserChatKey.ChatId,
                    BotResponse.ReplySentPrivately,
                    replyParameters: ToReplyParameters(update));
            }
            catch (ApiRequestException)
            {
                // Group ack is best-effort.
            }
        }

        private static ReplyParameters? ToReplyParameters(UpdateDecorator update) =>
            update.Message?.TelegramMessageId is long id
                ? new ReplyParameters { MessageId = (int)id }
                : null;

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
                await botClient.SendMessage(
                    chatId,
                    escapedText,
                    ParseMode.MarkdownV2,
                    replyParameters: replyToMessageId.HasValue
                        ? new ReplyParameters { MessageId = replyToMessageId.Value }
                        : null);
            }
        }

        public static async Task<bool> TrySendMarkdown2MessageAsync(
            this ITelegramBotClient botClient,
            long chatId,
            string text,
            int? replyToMessageId = null,
            int partsLimit = -1,
            ILogger? logger = null
        )
        {
            try
            {
                await SendMarkdown2MessageAsync(botClient, chatId, text, replyToMessageId);
                return true;
            }
            catch (ApiRequestException ex)
                when (ex.Message.Contains("can't parse entities"))
            {
                var shortReply = text.Truncate(30) + "...";
                logger?.LogInformation(ex, "Telegram returns error while parsing markdown in message: {Reply}. Trying to resend without markdown",
                    shortReply);
                await botClient.SendSplittedTextMessageAsync(chatId,
                    text, replyToMessageId:replyToMessageId);
            }

            return false;
        }

        public static async Task SendSplittedTextMessageAsync(
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
                await botClient.SendMessage(chatId, part, replyParameters: replyToMessageId);
            }
        }

        public static async Task SendOutOfFreeRequestsMessageAsync(this ITelegramBotClient botClient, long chatId,
            DateTimeOffset? nextRefreshExecution)
        {
            if (!nextRefreshExecution.HasValue || nextRefreshExecution.Value < DateTimeOffset.UtcNow)
            {
                await botClient.SendMessage(chatId, BotResponse.SimpleNoFreeRequests,
                    replyMarkup: TelegramBotUiService.DepositInlineKeyboard);

                return;
            }

            var timeTillRefresh = nextRefreshExecution.Value - DateTimeOffset.UtcNow;
            var message = string.Format(BotResponse.TimeNoFreeRequests, timeTillRefresh);

            await botClient.SendMessage(chatId, message,
                replyMarkup: TelegramBotUiService.DepositInlineKeyboard);
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
