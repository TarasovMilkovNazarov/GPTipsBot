using GPTipsBot.Resources;
using Microsoft.Extensions.Logging;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using OpenAI.Managers;
using OpenAI;
using OpenAI.ObjectModels.ResponseModels.ImageResponseModel;
using System.Drawing;
using GPTipsBot.Exceptions;

namespace GPTipsBot.Services
{
    public class ImageCreatorService
    {
        private readonly ILogger<ImageCreatorService> log;
        private readonly TokenQueue tokensQueue;
        public ImageCreatorService(ILogger<ImageCreatorService> log, TokenQueue tokensQueue)
        {
            this.log = log;
            this.tokensQueue = tokensQueue;
        }

        public async Task<List<string>> GenerateImage(string prompt)
        {
            var apiKey = await tokensQueue.GetTokenAsync();

            var openAiService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = apiKey
            });

            ImageCreateResponse imageResult = await openAiService.Image.CreateImage(new ImageCreateRequest
            {
                Prompt = prompt,
                N = 4,
                Size = StaticValues.ImageStatics.Size.Size1024,
                ResponseFormat = StaticValues.ImageStatics.ResponseFormat.Url,
                User = "TestUser"
            });

            tokensQueue.AddToken(apiKey);

            if (imageResult.Successful)
            {
                return imageResult.Results.Select(r => r.Url).ToList();
            }

            log.LogError("Failed to get images from DALL-E: [{Code}] {Message}, token: {Token}", imageResult.Error?.Code, imageResult.Error?.Message, apiKey[..10]);

            if (imageResult.Error?.Code == "content_policy_violation")
            {
                throw new ClientException(DalleResponse.BlockedPromptError);
            }

            throw new ClientException(BotResponse.SomethingWentWrongWithImageService);
        }
    }
}
