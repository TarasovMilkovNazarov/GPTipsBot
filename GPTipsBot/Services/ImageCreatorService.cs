using GPTipsBot.Config;
using GPTipsBot.Exceptions;
using GPTipsBot.Resources;
using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace GPTipsBot.Services;

public class ImageCreatorService(
    ILogger<ImageCreatorService> log,
    IOpenAIService openAiService)
{
    public async Task<byte[]> GenerateAsync(
        string prompt,
        string size,
        string quality,
        long chatId,
        CancellationToken cancellationToken = default)
    {
        var imageResult = await openAiService.Image.CreateImage(new ImageCreateRequest
        {
            Prompt = prompt,
            N = 1,
            Size = size,
            Quality = quality,
            OutputFormat = StaticValues.ImageStatics.OutputFormat.Png,
            User = chatId.ToString(),
            Model = GptImageConfig.ModelId,
        }, cancellationToken);

        return ExtractImageBytes(imageResult, chatId);
    }

    public async Task<byte[]> EditAsync(
        string prompt,
        byte[] imageBytes,
        string imageFileName,
        string size,
        string quality,
        long chatId,
        CancellationToken cancellationToken = default)
    {
        var imageResult = await openAiService.Image.CreateImageEdit(new ImageEditCreateRequest
        {
            Prompt = prompt,
            Image = imageBytes,
            ImageName = imageFileName,
            N = 1,
            Size = size,
            Quality = quality,
            OutputFormat = StaticValues.ImageStatics.OutputFormat.Png,
            User = chatId.ToString(),
            Model = GptImageConfig.ModelId,
        }, cancellationToken);

        return ExtractImageBytes(imageResult, chatId);
    }

    private byte[] ExtractImageBytes(OpenAI.ObjectModels.ResponseModels.ImageResponseModel.ImageCreateResponse imageResult, long chatId)
    {
        if (imageResult.Successful)
        {
            var b64 = imageResult.Results?.FirstOrDefault()?.B64;
            if (!string.IsNullOrWhiteSpace(b64))
            {
                return Convert.FromBase64String(b64);
            }

            log.LogError("GPT Image 2 returned success without b64_json");
            throw new ClientException(chatId, DalleResponse.BadImagesError);
        }

        if (imageResult.Error?.Code == "content_policy_violation")
        {
            throw new ClientException(chatId, DalleResponse.BlockedPromptError);
        }

        if (imageResult.Error?.Code == "rate_limit_exceeded")
        {
            throw new ClientException(chatId, DalleResponse.RateLimit);
        }

        log.LogError("Failed GPT Image 2 request: [{Code}] {Message}",
            imageResult.Error?.Code, imageResult.Error?.Message);

        throw new ClientException(chatId, DalleResponse.BadImagesError);
    }
}
