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
    private static string ResultGifPath => Path.Combine(AssetsDir, "result.gif");

    /// <summary>
    /// Sends a visual before → prompt → result example (from Alice demos), then the instructions.
    /// </summary>
    public static async Task SendAnimatePhotoInstructionsAsync(
        this ITelegramBotClient botClient,
        long chatId,
        ReplyMarkup? replyMarkup = null,
        int? messageThreadId = null,
        CancellationToken cancellationToken = default)
    {
        var resultPath = File.Exists(ResultMp4Path) ? ResultMp4Path
            : File.Exists(ResultGifPath) ? ResultGifPath
            : null;

        if (File.Exists(BeforePath) && resultPath is not null)
        {
            await using (var beforeStream = File.OpenRead(BeforePath))
            {
                await botClient.SendPhoto(
                    chatId,
                    InputFile.FromStream(beforeStream, "before.jpg"),
                    caption: BotResponse.AnimateExampleBeforeCaption,
                    messageThreadId: messageThreadId,
                    cancellationToken: cancellationToken);
            }

            await botClient.SendMessage(
                chatId,
                BotResponse.AnimateExamplePromptCaption,
                messageThreadId: messageThreadId,
                cancellationToken: cancellationToken);

            var fileName = Path.GetFileName(resultPath);
            await using (var resultStream = File.OpenRead(resultPath))
            {
                await botClient.SendAnimation(
                    chatId,
                    InputFile.FromStream(resultStream, fileName),
                    caption: BotResponse.AnimateExampleResultCaption,
                    messageThreadId: messageThreadId,
                    cancellationToken: cancellationToken);
            }
        }

        await botClient.SendMessage(
            chatId,
            BotResponse.SendPhotoToAnimate,
            replyMarkup: replyMarkup,
            messageThreadId: messageThreadId,
            cancellationToken: cancellationToken);
    }
}
