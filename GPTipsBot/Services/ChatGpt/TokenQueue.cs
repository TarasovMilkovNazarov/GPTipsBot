using GPTipsBot.Repositories;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Services.ChatGpt
{
    public class TokenQueue
    {
        private readonly ConcurrentQueue<string> tokens;
        private readonly SemaphoreSlim semaphore;

        public TokenQueue(OpenaiAccountsRepository openaiAccountsRepository)
        {
            var initialTokens = openaiAccountsRepository.GetAllAvailable().Select(x => x.Token);
            tokens = new ConcurrentQueue<string>(initialTokens);
            semaphore = new SemaphoreSlim(initialTokens.Count());
        }

        public async Task<string> GetTokenAsync()
        {
            TimeSpan timeout = TimeSpan.FromMinutes(3);

            if (await semaphore.WaitAsync(timeout))
            {
                if (tokens.TryDequeue(out var token))
                {
                    return token;
                }
                else
                {
                    throw new InvalidOperationException("Ошибка при извлечении токена из очереди.");
                }
            }
            else
            {
                throw new TimeoutException("Превышено время ожидания токена.");
            }
        }

        public void AddToken(string token)
        {
            tokens.Enqueue(token);
            semaphore.Release();
        }
    }
}
