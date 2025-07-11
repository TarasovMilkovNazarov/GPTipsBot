using GPTipsBot.Repositories;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Services
{
    public class TokenQueue : IDisposable
    {
        private readonly ILogger<TokenQueue> _logger;
        private readonly ConcurrentQueue<string> _tokens;
        private readonly SemaphoreSlim _semaphore;

        public TokenQueue(OpenaiAccountsRepository openaiAccountsRepository, ILogger<TokenQueue> logger)
        {
            _logger = logger;
            var initialTokens = openaiAccountsRepository.GetAllAvailable().Select(x => x.Token).ToList();
            _tokens = new ConcurrentQueue<string>(initialTokens);
            _semaphore = new SemaphoreSlim(initialTokens.Count);
        }

        public async Task<string> GetTokenAsync()
        {
            if (AppConfig.IsDevelopment && AppConfig.DebugOpenAiApiKey is not null)
                return AppConfig.DebugOpenAiApiKey;

            _logger.LogInformation("Tokens count {tokensCount}", _tokens.Count);
            if (await _semaphore.WaitAsync(TimeSpan.FromMinutes(3)))
            {
                if (_tokens.TryDequeue(out var token))
                {
                    _logger.LogInformation("Получили токен {Token}***", token[..10]);
                    return token;
                }

                throw new InvalidOperationException("Ошибка при извлечении токена из очереди.");
            }

            throw new TimeoutException("Превышено время ожидания токена.");
        }

        public void AddToken(string token)
        {
            _tokens.Enqueue(token);
            _logger.LogInformation("Tokens count {tokensCount}", _tokens.Count);
            _semaphore.Release();
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}
