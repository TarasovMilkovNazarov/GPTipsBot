﻿using GPTipsBot.Resources;
using Microsoft.Extensions.Logging;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using OpenAI.Managers;
using OpenAI;
using OpenAI.ObjectModels.ResponseModels.ImageResponseModel;
using System.Drawing;
using GPTipsBot.Exceptions;
using System.Net;

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

            //uncomment for sniffing requests in fiddler
            //ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
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
                N = 1,
                Size = StaticValues.ImageStatics.Size.Size1024,
                ResponseFormat = StaticValues.ImageStatics.ResponseFormat.Url,
                User = "TestUser",
                Model = "dall-e-3"
            });

            tokensQueue.AddToken(apiKey);

            if (imageResult.Successful)
            {
                return imageResult.Results.Select(r => r.Url).ToList();
            }

            if (imageResult.Error?.Code == "content_policy_violation")
            {
                throw new ClientException(DalleResponse.BlockedPromptError);
            }
            if (imageResult.Error?.Code == "rate_limit_exceeded")
            {
                throw new ClientException(DalleResponse.RateLimit);
            }

            log.LogError("Failed to get images from DALL-E: [{Code}] {Message}, token: {Token}", imageResult.Error?.Code, imageResult.Error?.Message, apiKey[..10]);

            throw new ClientException(BotResponse.SomethingWentWrongWithImageService);
        }
    }
}
