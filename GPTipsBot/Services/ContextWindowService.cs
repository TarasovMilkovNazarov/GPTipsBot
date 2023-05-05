using GPTipsBot.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Services
{
    public class ContextWindow
    {
        private LinkedList<string> messages;
        private int windowSize;
        private readonly ChatGptService chatGptService;
        private long tokensCount;

        public ContextWindow(ChatGptService chatGptService, int size = 5)
        {
            messages = new LinkedList<string>();
            windowSize = size;
            this.chatGptService = chatGptService;
            tokensCount = 0;
        }

        public void AddMessage(string message)
        {
            if (messages.Count < windowSize)
            {
                var messageTokensCount = chatGptService.CountTokens(message);
                if (messageTokensCount + tokensCount <= ChatGptService.MaxTokensLimit)
                {
                    tokensCount += messageTokensCount;
                    messages.AddFirst(message);
                    return;
                }
            }
        }

        public string GetContext()
        {
            return string.Join(Environment.NewLine, messages);
        }

        public static string TryToPruneMessage(string message)
        {
            throw new NotImplementedException();

            return message;
        }
    }
}
