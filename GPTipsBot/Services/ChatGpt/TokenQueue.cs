﻿using GPTipsBot.Repositories;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Services.ChatGpt
{
    public class TokenQueue
    {
        private readonly ILogger<TokenQueue> logger;
        private readonly ConcurrentQueue<string> tokens;
        private readonly SemaphoreSlim semaphore;

        public TokenQueue(OpenaiAccountsRepository openaiAccountsRepository, ILogger<TokenQueue> logger)
        {
            this.logger = logger;
            var initialTokens = openaiAccountsRepository.GetAllAvailable().Select(x => x.Token).ToList();
            tokens = new ConcurrentQueue<string>(initialTokens);
            semaphore = new SemaphoreSlim(initialTokens.Count);
        }

        public async Task<string> GetTokenAsync()
        {
            if (AppConfig.IsDevelopment && AppConfig.DebugOpenAiToken is not null)
                return AppConfig.DebugOpenAiToken;
            
            logger.LogInformation("Получаем токен из базы");
            if (await semaphore.WaitAsync(TimeSpan.FromMinutes(3)))
            {
                if (tokens.TryDequeue(out var token))
                {
                    logger.LogInformation($"Получили токен {token[..10]}");
                    return token;
                }

                throw new InvalidOperationException("Ошибка при извлечении токена из очереди.");
            }

            throw new TimeoutException("Превышено время ожидания токена.");
        }

        public void AddToken(string token)
        {
            tokens.Enqueue(token);
            semaphore.Release();
        }
    }
}
