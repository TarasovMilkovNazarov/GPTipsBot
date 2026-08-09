using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using GPTipsBot.Services.YandexCloud;

namespace GPTipsBot.Web;

public class WebMediaService(
    UserService userService,
    MessageRepository messageRepository,
    ImageCreatorService imageCreatorService,
    ITextRecognizer textRecognizer,
    SpeechToTextService speechToTextService)
{
    public async Task<WebImageResult> GenerateImageAsync(
        long userId,
        string prompt,
        string? size,
        string? quality,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException("Prompt is required");
        }

        var resolvedSize = GptImageConfig.ResolveSize(size).Id;
        var resolvedQuality = GptImageConfig.ResolveQuality(quality);
        var hold = await userService.TryReserveGptImageAsync(userId, resolvedQuality.StarsCost);
        if (hold is null)
        {
            throw new InsufficientQuotaException(
                $"Not enough Stars for image generation ({resolvedQuality.StarsCost}⭐ required).");
        }

        try
        {
            var bytes = await imageCreatorService.GenerateAsync(
                prompt.Trim(),
                resolvedSize,
                resolvedQuality.Id,
                userId,
                cancellationToken);

            var chatId = userId;
            await messageRepository.AddAsync(new MessageDto(new UserChatKey(userId, chatId))
            {
                Text = prompt.Trim(),
                Role = MessageOwner.User,
                ContextBound = false,
                BotMessageType = BotMessageType.ImagePrompt,
            });
            await messageRepository.AddAsync(new MessageDto(new UserChatKey(userId, chatId))
            {
                Text = $"[image:{resolvedSize}/{resolvedQuality.Id}]",
                Role = MessageOwner.Assistant,
                ContextBound = false,
                BotMessageType = BotMessageType.ImageGenerated,
            });

            await userService.ConfirmAsync(hold.Id);
            return new WebImageResult(Convert.ToBase64String(bytes), "image/png", resolvedQuality.StarsCost);
        }
        catch
        {
            await userService.ReleaseAsync(hold.Id);
            throw;
        }
    }

    public async Task<string> RecognizeTextAsync(long userId, byte[] imageBytes, CancellationToken cancellationToken)
    {
        if (imageBytes.Length == 0)
        {
            throw new InvalidOperationException("Empty image");
        }

        var hold = await userService.TryReserveTextRecognitionAsync(userId);
        if (hold is null)
        {
            throw new InsufficientQuotaException("OCR free quota exhausted or insufficient Stars.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = await textRecognizer.Recognize(Convert.ToBase64String(imageBytes));
            await messageRepository.AddAsync(new MessageDto(new UserChatKey(userId, userId))
            {
                Text = text,
                Role = MessageOwner.Assistant,
                ContextBound = false,
                BotMessageType = BotMessageType.RecognizeText,
            });
            await userService.ConfirmAsync(hold.Id);
            return text;
        }
        catch
        {
            await userService.ReleaseAsync(hold.Id);
            throw;
        }
    }

    public Task<string> TranscribeAsync(byte[] audioBytes, CancellationToken cancellationToken) =>
        speechToTextService.RecognizeAudioAsync(audioBytes, cancellationToken);
}

public sealed record WebImageResult(string Base64, string MimeType, double StarsCharged);

public class InsufficientQuotaException(string message) : Exception(message);
