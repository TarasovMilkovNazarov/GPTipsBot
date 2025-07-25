using GPTipsBot.Resources;
using Microsoft.Extensions.Logging;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using GPTipsBot.Exceptions;

namespace GPTipsBot.Services
{
    public class ImageCreatorService(
        ILogger<ImageCreatorService> log,
        TokenQueue tokensQueue,
        OpenAiServiceCreator openAiServiceCreator)
    {
        public async Task<List<string>> GenerateImage(string prompt, long chatId)
        {
            var apiKey = await tokensQueue.GetTokenAsync();

            var openAiService = openAiServiceCreator.Create(apiKey);

            var imageResult = await openAiService.Image.CreateImage(new ImageCreateRequest
            {
                Prompt = prompt,
                N = 2,
                Size = StaticValues.ImageStatics.Size.Size1024,
                ResponseFormat = StaticValues.ImageStatics.ResponseFormat.Url,
                User = "TestUser",
                Model = "dall-e-2"
            });

            tokensQueue.AddToken(apiKey);

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

            log.LogError("Failed to get images from DALL-E: [{Code}] {Message}, token: {Token}",
                imageResult.Error?.Code, imageResult.Error?.Message, apiKey[..10]);

            throw new ClientException(chatId, BotResponse.SomethingWentWrongWithImageService);
        }
    }
}
