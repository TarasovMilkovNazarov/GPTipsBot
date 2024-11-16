using OpenAI;
using OpenAI.Managers;
using GptModels = OpenAI.ObjectModels;

namespace GPTipsBot.Services
{
    public class ProxyApiService : OpenAiServiceCreator
    {
        private readonly string _token;

        public ProxyApiService()
        {
            _token = AppConfig.ProxyApiApiKey;
        }

        public override OpenAIService Create(string token)
        {
            var openAiService = new OpenAIService(new OpenAiOptions()
            {
                BaseDomain = "https://api.proxyapi.ru/openai/v1",
                ApiKey = token,
                DefaultModelId = GptModels.Models.Gpt_3_5_Turbo_0125
            });

            return openAiService;
        }

        public override Task<string> GetApiKeyAsync()
        {
            return Task.FromResult(_token);
        }

        public override void ReturnApiKey(string apiKey)
        {
            
        }
    }
}
