using GPTipsBot.Config;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Managers;

namespace GPTipsBot.Services
{
    public class ProxyApiService(ILogger<ProxyApiService> logger) : OpenAiServiceCreator
    {
        private readonly ILogger<ProxyApiService> _logger = logger;
        private readonly string _token = AppConfig.ProxyApiApiKey;

        public override OpenAIService Create(string token)
        {
            var clientHandler = new HttpClientHandler()
            {
                // ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
            };
            var loggingHandler = new LoggingHandler()
            {
                InnerHandler = clientHandler
            };

            var httpClient = new HttpClient(loggingHandler);
            // httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json; charset=utf-8");

            var openAiService = new OpenAIService(new OpenAiOptions()
            {
                BaseDomain = "https://api.vsegpt.ru/v1",
                ApiKey = token,
                DefaultModelId = "openai/gpt-3.5-turbo",
            }, httpClient);

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

    class LoggingHandler : DelegatingHandler
    {
        // protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        // {
        //     // Log the request details
        //     Console.WriteLine($"Request: {request.Method} {request.RequestUri}");
        //
        //     if (request.Content != null)
        //     {
        //         string requestBody = await request.Content.ReadAsStringAsync();
        //         Console.WriteLine($"Request Content: {requestBody}");
        //     }
        //
        //     // Log the request headers
        //     foreach(var header in request.Headers)
        //     {
        //         Console.WriteLine($"Header: {header.Key} - {string.Join(",", header.Value)}");
        //     }
        //
        //     // Forward the request to the inner handler
        //     HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        //
        //     // Log the response details
        //     Console.WriteLine($"Response: {response.StatusCode}");
        //
        //     if (response.Content != null)
        //     {
        //         string responseBody = await response.Content.ReadAsStringAsync();
        //         Console.WriteLine($"Response Content: {responseBody}");
        //     }
        //
        //     return response;
        // }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
            {
                var requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            var response = await base.SendAsync(request, cancellationToken);

            return response;
        }
    }
}
