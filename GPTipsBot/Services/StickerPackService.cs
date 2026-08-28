using System.Collections.Concurrent;
using GPTipsBot.Config;
using GPTipsBot.Resources;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.Services;

public class StickerPackService(
    ImageCreatorService imageCreatorService,
    ITelegramBotClient botClient,
    ILogger<StickerPackService> log)
{
    public async Task<StickerDraft> GenerateHeroAsync(
        string? sourcePhotoBase64,
        string? description,
        long chatId,
        CancellationToken cancellationToken)
    {
        byte[] raw;
        if (!string.IsNullOrWhiteSpace(sourcePhotoBase64))
        {
            var sourceBytes = Convert.FromBase64String(sourcePhotoBase64);
            raw = await imageCreatorService.EditAsync(
                StickerPackConfig.HeroFromPhotoPrompt(description),
                sourceBytes,
                "photo.png",
                GptImageConfig.SizeSquare,
                GptImageConfig.QualityMedium,
                chatId,
                cancellationToken,
                StickerPackConfig.TransparentBackground);
        }
        else
        {
            raw = await imageCreatorService.GenerateAsync(
                StickerPackConfig.HeroFromTextPrompt(description ?? "a cute original character"),
                GptImageConfig.SizeSquare,
                GptImageConfig.QualityMedium,
                chatId,
                cancellationToken,
                StickerPackConfig.TransparentBackground);
        }

        var hero = StickerPackConfig.Emotions[0];
        return new StickerDraft
        {
            Emoji = hero.Emoji,
            Keyword = hero.Keyword,
            Png = StickerImageProcessor.ToTelegramPng(raw),
        };
    }

    public async Task<IReadOnlyList<StickerDraft>> GenerateVariantsAsync(
        byte[] heroPng,
        IProgress<int>? progress,
        long chatId,
        CancellationToken cancellationToken)
    {
        var variants = StickerPackConfig.Emotions.Where(e => !e.IsHero).ToArray();
        var results = new ConcurrentDictionary<int, StickerDraft>();
        using var gate = new SemaphoreSlim(StickerPackConfig.VariantParallelism);

        await Task.WhenAll(variants.Select(async (emotion, index) =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var raw = await imageCreatorService.EditAsync(
                    StickerPackConfig.VariantPrompt(emotion),
                    heroPng,
                    "hero.png",
                    GptImageConfig.SizeSquare,
                    GptImageConfig.QualityLow,
                    chatId,
                    cancellationToken,
                    StickerPackConfig.TransparentBackground);

                results[index] = new StickerDraft
                {
                    Emoji = emotion.Emoji,
                    Keyword = emotion.Keyword,
                    Png = StickerImageProcessor.ToTelegramPng(raw),
                };
                progress?.Report(results.Count + 1);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Sticker variant {Emoji} failed for chat {ChatId}", emotion.Emoji, chatId);
            }
            finally
            {
                gate.Release();
            }
        }));

        return variants
            .Select((_, i) => results.TryGetValue(i, out var draft) ? draft : null)
            .Where(d => d != null)
            .Select(d => d!)
            .ToList();
    }

    public async Task<string> PublishAsync(
        long telegramUserId,
        string title,
        IReadOnlyList<StickerDraft> stickers,
        CancellationToken cancellationToken)
    {
        var botUsername = string.IsNullOrWhiteSpace(AppConfig.BotName)
            ? (await botClient.GetMe(cancellationToken)).Username
            : AppConfig.BotName;
        var name = StickerPackConfig.BuildSetName(telegramUserId, botUsername);

        var streams = new List<MemoryStream>(stickers.Count);
        try
        {
            var inputStickers = stickers.Select((sticker, i) =>
            {
                var stream = new MemoryStream(sticker.Png);
                streams.Add(stream);
                return new InputSticker(
                    InputFile.FromStream(stream, $"sticker{i}.png"),
                    StickerFormat.Static,
                    [sticker.Emoji])
                {
                    Keywords = BuildKeywords(sticker.Keyword, botUsername),
                };
            }).ToArray();

            await botClient.CreateNewStickerSet(
                telegramUserId,
                name,
                StickerPackConfig.BuildSetTitle(title, botUsername),
                inputStickers,
                stickerType: StickerType.Regular,
                cancellationToken: cancellationToken);
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }

        return $"https://t.me/addstickers/{name}";
    }

    public static string IntroText => string.Format(
        BotResponse.StickerPackIntro,
        StickerPackConfig.HeroStars,
        StickerPackConfig.PackRemainderStars,
        StickerPackConfig.ExamplePackUrl);

    private string? _exampleStickerFileId;

    public async Task TrySendExampleAsync(
        long chatId,
        int? messageThreadId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fileId = _exampleStickerFileId;
            if (string.IsNullOrEmpty(fileId))
            {
                var set = await botClient.GetStickerSet(StickerPackConfig.ExampleSetName, cancellationToken);
                fileId = set.Stickers.FirstOrDefault()?.FileId;
                if (string.IsNullOrEmpty(fileId))
                {
                    return;
                }

                _exampleStickerFileId = fileId;
            }

            await botClient.SendSticker(
                chatId,
                InputFile.FromFileId(fileId),
                messageThreadId: messageThreadId,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            log.LogDebug(ex, "Failed to send example sticker from {SetName}", StickerPackConfig.ExampleSetName);
        }
    }

    private static string[] BuildKeywords(string keyword, string? botUsername)
    {
        var bot = (botUsername ?? "GPTipsBot").Trim().TrimStart('@');
        if (string.IsNullOrEmpty(bot) ||
            keyword.Equals(bot, StringComparison.OrdinalIgnoreCase))
        {
            return [keyword];
        }

        return [keyword, bot];
    }
}
