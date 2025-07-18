using System.ClientModel;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace GPTipsBot.Services
{
    public class ProxyApiService : OpenAiServiceCreator
    {
        private readonly ILogger<ProxyApiService> _logger;
        private readonly string _token;

        public ProxyApiService(ILogger<ProxyApiService> logger)
        {
            _logger = logger;
            _token = AppConfig.ProxyApiApiKey;
        }

        public override OpenAIClient Create(string token)
        {

            var openAiService = new OpenAIClient(new ApiKeyCredential(AppConfig.ProxyApiApiKey), new OpenAIClientOptions()
            {
                Endpoint = new Uri("https://api.vsegpt.ru/v1"),

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
