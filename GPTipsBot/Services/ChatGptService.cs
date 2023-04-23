using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TiktokenSharp;

namespace GPTipsBot.Services
{
    public class ChatGptService
    {
        private readonly string openaiBaseUrl = "https://api.openai.com/v1/engines/davinci-codex";

        public ChatGptService() { }

        public static long CountTokens(string message = "antidisestablishmentarianism")
        {
            TikToken tikToken = TikToken.EncodingForModel("gpt-3.5-turbo");
            var i = tikToken.Encode(message); //[15339, 1917]

            return i.Count;
        }
    }
}
