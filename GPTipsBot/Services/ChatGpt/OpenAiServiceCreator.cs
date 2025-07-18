using OpenAI;

namespace GPTipsBot.Services
{
    public abstract class OpenAiServiceCreator
    {
        abstract public OpenAIClient Create(string token);
        abstract public Task<string> GetApiKeyAsync();
        abstract public void ReturnApiKey(string apiKey);
    }
}
