using OpenAI.Managers;

namespace GPTipsBot.Services
{
    public abstract class OpenAiServiceCreator
    {
        abstract public OpenAIService Create(string token);
        abstract public Task<string> GetApiKeyAsync();
        abstract public void ReturnApiKey(string apiKey);
    }
}
