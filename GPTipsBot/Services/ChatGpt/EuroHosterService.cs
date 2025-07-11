using OpenAI;
using OpenAI.Managers;
using GptModels = OpenAI.ObjectModels;

namespace GPTipsBot.Services
{
    public class EuroHosterService : OpenAiServiceCreator
    {
        private readonly TokenQueue _apiKeyQueue;

        public EuroHosterService(TokenQueue tokenQueue)
        {
            _apiKeyQueue = tokenQueue;
        }

        public override OpenAIService Create(string token)
        {
            var openAiService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = token,
                DefaultModelId = GptModels.Models.Gpt_3_5_Turbo
            }, new EuroHosterHttpClient());

            return openAiService;
        }

        public override async Task<string> GetApiKeyAsync()
        {
            return await _apiKeyQueue.GetTokenAsync();
        }

        public override void ReturnApiKey(string apiKey)
        {
            _apiKeyQueue.AddToken(apiKey);
        }
    }
}
