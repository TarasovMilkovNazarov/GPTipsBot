using GPTipsBot.Exceptions;
using GPTipsBot.Resources;
using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace GPTipsBot.Services
{
    public class ImageCreatorService(
        ILogger<ImageCreatorService> log,
        IOpenAIService openAiService)
    {
        public async Task<List<string>> GenerateImage(string prompt, long chatId)
        {
            var imageResult = await openAiService.Image.CreateImage(new ImageCreateRequest
            {
                Prompt = prompt,
                N = 2,
                Size = StaticValues.ImageStatics.Size.Size1024,
                ResponseFormat = StaticValues.ImageStatics.ResponseFormat.Url,
                User = "TestUser",
                Model = "dall-e-2"
            });

            if (imageResult.Successful)
            {
                return imageResult.Results.Select(r => r.Url).ToList();
            }

            if (imageResult.Error?.Code == "content_policy_violation")
            {
                throw new ClientException(chatId, DalleResponse.BlockedPromptError);
            }
            if (imageResult.Error?.Code == "rate_limit_exceeded")
            {
                throw new ClientException(chatId, DalleResponse.RateLimit);
            }

            log.LogError("Failed to get images from DALL-E: [{Code}] {Message}",
                imageResult.Error?.Code, imageResult.Error?.Message);

            throw new ClientException(chatId, DalleResponse.BadImagesError);
        }
    }
}
