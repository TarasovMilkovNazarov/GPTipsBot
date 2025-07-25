using OpenAI;
using OpenAI.Managers;
using GptModels = OpenAI.ObjectModels;

namespace GPTipsBot.Services
{
    public class EuroHosterService(TokenQueue tokenQueue) : OpenAiServiceCreator
    {
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
            return await tokenQueue.GetTokenAsync();
        }

        public override void ReturnApiKey(string apiKey)
        {
            tokenQueue.AddToken(apiKey);
        }
    }
}
