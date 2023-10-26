using TiktokenSharp;

namespace GPTipsBot.Services
{
    public class ChatGptService
    {
        public ChatGptService() { }

        public long CountTokens(string message)
        {
            TikToken tikToken = TikToken.EncodingForModel("gpt-3.5-turbo");
            var i = tikToken.Encode(message); //[15339, 1917]

            return i.Count;
        }
    }
}