using GPTipsBot.Resources;
using GPTipsBot.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Extensions;

public static class AnimateExampleExtensions
{
    private static readonly string AssetsDir = Path.Combine(
        AppContext.BaseDirectory,
        "Assets",
        "AnimateExample");

    private static string ResultMp4Path => Path.Combine(AssetsDir, "result.mp4");

    /// <summary>
    /// Sends one message: Alice example animation + instructions caption + cancel markup.
    /// </summary>
    public static async Task SendAnimatePhotoInstructionsAsync(
        this ITelegramBotClient botClient,
        long chatId,
        ReplyMarkup? replyMarkup = null,
        int? messageThreadId = null,
        CancellationToken cancellationToken = default)
    {
        if (File.Exists(ResultMp4Path))
        {
            await using var resultStream = File.OpenRead(ResultMp4Path);
            if (chatId < 0 || replyMarkup is not InlineKeyboardMarkup inline)
            {
                await botClient.SendAnimation(
                    chatId,
                    InputFile.FromStream(resultStream, "result.mp4"),
                    caption: BotResponse.SendPhotoToAnimate,
                    replyMarkup: replyMarkup ?? (chatId > 0 ? TelegramBotUiService.StartKeyboard : null),
                    messageThreadId: messageThreadId,
                    cancellationToken: cancellationToken);
                return;
            }

            var sent = await botClient.SendAnimation(
                chatId,
                InputFile.FromStream(resultStream, "result.mp4"),
                caption: BotResponse.SendPhotoToAnimate,
                replyMarkup: TelegramBotUiService.StartKeyboard,
                messageThreadId: messageThreadId,
                cancellationToken: cancellationToken);
            try
            {
                await botClient.EditMessageReplyMarkup(chatId, sent.MessageId, inline, cancellationToken: cancellationToken);
            }
            catch (ApiRequestException)
            {
            }

            return;
        }

        await botClient.SendMessageWithMenuAsync(
            chatId,
            BotResponse.SendPhotoToAnimate,
            replyMarkup,
            chatId < 0,
            messageThreadId,
            cancellationToken: cancellationToken);
    }
}
