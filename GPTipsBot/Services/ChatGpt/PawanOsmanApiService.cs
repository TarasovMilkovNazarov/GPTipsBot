using OpenAI;
using OpenAI.Managers;

namespace GPTipsBot.Services
{
    /// <summary>
    /// https://github.com/PawanOsman/ChatGPT
    /// </summary>
    public class PawanOsmanApiService : OpenAiServiceCreator
    {
        private readonly string _token;

        public PawanOsmanApiService()
        {
            _token = AppConfig.PawanOsmanApiKey;
        }

        public override OpenAIService Create(string token)
        {
            //var handler = new FiddlerProxyClientHandler();
            //HttpClient httpClient = new HttpClient(handler);

            var openAiService = new OpenAIService(new OpenAiOptions()
            {
                BaseDomain = "https://api.pawan.krd/pai-001/v1",
                ApiKey = token,
                DefaultModelId = "pai-001"
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
