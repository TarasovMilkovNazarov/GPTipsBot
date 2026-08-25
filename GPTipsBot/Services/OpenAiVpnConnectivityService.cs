using System.Diagnostics;
using GPTipsBot.Config;
using GPTipsBot.Dtos;
using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels.RequestModels;

namespace GPTipsBot.Services;

public class OpenAiVpnConnectivityService(
    IOpenAIService openAiService,
    ILogger<OpenAiVpnConnectivityService> logger)
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public async Task<OpenAiVpnConnectivityResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var proxyEndpoint = $"{AppConfig.HappProxyIp}:{AppConfig.HappProxyPort}";
        var stopwatch = Stopwatch.StartNew();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);

        try
        {
            var response = await openAiService.ChatCompletion.CreateCompletion(
                new ChatCompletionCreateRequest
                {
                    Messages = [new ChatMessage("user", "ping")],
                    MaxTokens = 1
                },
                cancellationToken: timeoutCts.Token);

            stopwatch.Stop();

            if (response.Successful)
            {
                logger.LogInformation("OpenAI VPN connectivity check succeeded via proxy {Proxy}", proxyEndpoint);
                return new OpenAiVpnConnectivityResult(true, proxyEndpoint, null, stopwatch.Elapsed);
            }

            var error = response.Error?.Message ?? "Unknown OpenAI API error";
            logger.LogWarning("OpenAI VPN connectivity check failed via proxy {Proxy}: {Error}", proxyEndpoint, error);
            return new OpenAiVpnConnectivityResult(false, proxyEndpoint, error, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "OpenAI VPN connectivity check failed via proxy {Proxy}", proxyEndpoint);

            var hint = ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase)
                ? "Проверьте: mihomo запущен на VPS, HTTP-прокси доступен на порту 10809."
                : ex.Message;

            return new OpenAiVpnConnectivityResult(false, proxyEndpoint, hint, stopwatch.Elapsed);
        }
    }

    public static string FormatResultMessage(OpenAiVpnConnectivityResult result)
    {
        var status = result.IsSuccess ? "OK" : "FAIL";
        var message = $"""
                    #vpn_openai_check
                    Proxy: {result.ProxyEndpoint}
                    OpenAI: {status}
                    Duration: {result.Duration.TotalMilliseconds:F0} ms
                    """;

        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorMessage))
            message += Environment.NewLine + $"Error: {result.ErrorMessage}";

        return message;
    }
}
