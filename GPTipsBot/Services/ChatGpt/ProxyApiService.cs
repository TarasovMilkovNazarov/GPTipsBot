using OpenAI;
using OpenAI.Managers;

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
                BaseDomain = "https://api.vsegpt.ru/v1",
                ApiKey = token,
                DefaultModelId = "openai/gpt-3.5-turbo",
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
