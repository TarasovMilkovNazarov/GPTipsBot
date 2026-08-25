using GPTipsBot.Config;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace GPTipsBot.Services;

public static class StickerImageProcessor
{
    public static byte[] ToTelegramPng(byte[] imageBytes)
    {
        using var image = Image.Load(imageBytes);
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(StickerPackConfig.TargetSide, StickerPackConfig.TargetSide),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3,
        }));

        using var output = new MemoryStream();
        image.Save(output, new PngEncoder
        {
            ColorType = PngColorType.RgbWithAlpha,
            CompressionLevel = PngCompressionLevel.BestCompression,
        });
        return output.ToArray();
    }
}
