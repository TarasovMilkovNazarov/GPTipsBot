using GPTipsBot.Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
// Not the whole Telegram.Bot.Types namespace: its Color clashes with ImageSharp's.
using InputFile = Telegram.Bot.Types.InputFile;

namespace GPTipsBot.Extensions;

/// <summary>
/// Try-on instructions with a before/after example: person.jpg [+ clothes.jpg] + result.jpg
/// from Assets/TryOnExample are glued into one picture, sent with the instructions as caption.
/// Without the files the instructions go out as plain text.
/// </summary>
public static class TryOnExampleExtensions
{
    private const int CollageHeight = 768;
    private const int Gap = 12;

    private static readonly string AssetsDir = Path.Combine(
        AppContext.BaseDirectory,
        "Assets",
        "TryOnExample");

    private static readonly Lazy<byte[]?> Collage = new(BuildCollage);

    /// <summary>Telegram file id of the collage after the first upload, so it's sent only once.</summary>
    private static string? _uploadedFileId;

    public static async Task SendTryOnInstructionsAsync(
        this ITelegramBotClient botClient,
        long chatId,
        string text,
        InlineKeyboardMarkup replyMarkup,
        int? messageThreadId = null,
        CancellationToken cancellationToken = default)
    {
        var collage = Collage.Value;
        if (collage == null)
        {
            await botClient.SendMessageWithMenuAsync(
                chatId, text, replyMarkup, chatId < 0, messageThreadId, cancellationToken: cancellationToken);
            return;
        }

        var caption = $"{BotResponse.TryOnExampleLegend}\n\n{text}";
        if (_uploadedFileId != null)
        {
            await botClient.SendPhoto(chatId, InputFile.FromFileId(_uploadedFileId), caption: caption,
                replyMarkup: replyMarkup, messageThreadId: messageThreadId, cancellationToken: cancellationToken);
            return;
        }

        await using var stream = new MemoryStream(collage);
        var message = await botClient.SendPhoto(chatId, InputFile.FromStream(stream, "try-on-example.jpg"),
            caption: caption, replyMarkup: replyMarkup, messageThreadId: messageThreadId,
            cancellationToken: cancellationToken);
        _uploadedFileId = message.Photo?.LastOrDefault()?.FileId;
    }

    private static byte[]? BuildCollage()
    {
        // Needs at least "before" and "after"; the clothes photo in between is optional.
        if (!File.Exists(Path.Combine(AssetsDir, "person.jpg")) || !File.Exists(Path.Combine(AssetsDir, "result.jpg")))
        {
            return null;
        }

        var paths = new[] { "person.jpg", "clothes.jpg", "result.jpg" }
            .Select(name => Path.Combine(AssetsDir, name))
            .Where(File.Exists)
            .ToList();

        try
        {
            var images = paths.Select(path =>
            {
                var image = Image.Load<Rgb24>(path);
                image.Mutate(ctx => ctx.AutoOrient().Resize(0, CollageHeight));
                return image;
            }).ToList();

            try
            {
                var width = images.Sum(i => i.Width) + Gap * (images.Count - 1);
                using var collage = new Image<Rgb24>(width, CollageHeight, Color.White.ToPixel<Rgb24>());
                var x = 0;
                foreach (var image in images)
                {
                    var left = x;
                    collage.Mutate(ctx => ctx.DrawImage(image, new Point(left, 0), 1f));
                    x += image.Width + Gap;
                }

                using var output = new MemoryStream();
                collage.Save(output, new JpegEncoder { Quality = 90 });
                return output.ToArray();
            }
            finally
            {
                images.ForEach(i => i.Dispose());
            }
        }
        catch (Exception)
        {
            // A broken example must not break the feature: fall back to text-only instructions.
            return null;
        }
    }
}
