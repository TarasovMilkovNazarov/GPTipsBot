using GPTipsBot.Resources;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Extensions;

public static class AnimateExampleExtensions
{
    private static readonly string AssetsDir = Path.Combine(
        AppContext.BaseDirectory,
        "Assets",
        "AnimateExample");

    private static string BeforePath => Path.Combine(AssetsDir, "before.jpg");
    private static string ResultMp4Path => Path.Combine(AssetsDir, "result.mp4");

    /// <summary>
    /// Sends a single album message: before photo + result video with instructions caption.
    /// </summary>
    public static async Task SendAnimatePhotoInstructionsAsync(
        this ITelegramBotClient botClient,
        long chatId,
        ReplyMarkup? replyMarkup = null,
        int? messageThreadId = null,
        CancellationToken cancellationToken = default)
    {
        if (File.Exists(BeforePath) && File.Exists(ResultMp4Path))
        {
            await using var beforeStream = File.OpenRead(BeforePath);
            await using var resultStream = File.OpenRead(ResultMp4Path);

            IAlbumInputMedia[] media =
            [
                new InputMediaPhoto(InputFile.FromStream(beforeStream, "before.jpg"))
                {
                    Caption = BotResponse.SendPhotoToAnimate,
                },
                new InputMediaVideo(InputFile.FromStream(resultStream, "result.mp4")),
            ];

            var messages = await botClient.SendMediaGroup(
                chatId,
                media,
                messageThreadId: messageThreadId,
                cancellationToken: cancellationToken);

            if (replyMarkup is InlineKeyboardMarkup inlineKeyboard && messages.Length > 0)
            {
                await botClient.EditMessageReplyMarkup(
                    chatId,
                    messages[0].Id,
                    replyMarkup: inlineKeyboard,
                    cancellationToken: cancellationToken);
            }

            return;
        }

        await botClient.SendMessage(
            chatId,
            BotResponse.SendPhotoToAnimate,
            replyMarkup: replyMarkup,
            messageThreadId: messageThreadId,
            cancellationToken: cancellationToken);
    }
}
