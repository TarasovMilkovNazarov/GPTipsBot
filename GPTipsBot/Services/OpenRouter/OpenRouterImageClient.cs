using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GPTipsBot.Config;
using GPTipsBot.Exceptions;
using GPTipsBot.Resources;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Services.OpenRouter;

/// <summary>
/// OpenRouter image-to-image via POST /api/v1/images.
/// Source photo goes in <c>input_references</c> as a data URI.
/// </summary>
public class OpenRouterImageClient(ILogger<OpenRouterImageClient> log, HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<byte[]> EditAsync(
        string model,
        string prompt,
        string imageBase64,
        long chatId,
        CancellationToken cancellationToken = default)
    {
        if (!WatermarkRemovalConfig.IsEnabled)
        {
            log.LogError("OPENROUTER_API_KEY is not configured");
            throw new ClientException(chatId, DalleResponse.BadImagesError);
        }

        var payload = new OpenRouterImageRequest
        {
            Model = model,
            Prompt = prompt,
            AspectRatio = "auto",
            OutputFormat = "jpeg",
            InputReferences =
            [
                new OpenRouterInputReference
                {
                    Type = "image_url",
                    ImageUrl = new OpenRouterImageUrl
                    {
                        Url = $"data:image/jpeg;base64,{imageBase64}",
                    },
                },
            ],
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "images")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppConfig.OpenRouterApiKey);
        request.Headers.TryAddWithoutValidation("HTTP-Referer", WatermarkRemovalConfig.Referer);
        request.Headers.TryAddWithoutValidation("X-Title", "GPTipsBot");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw ToClientException(response.StatusCode, body, model, chatId);
        }

        var parsed = JsonSerializer.Deserialize<OpenRouterImageResponse>(body, JsonOptions);
        var b64 = parsed?.Data?.FirstOrDefault()?.B64Json;
        if (string.IsNullOrWhiteSpace(b64))
        {
            log.LogError("OpenRouter {Model} returned no image", model);
            throw new ClientException(chatId, DalleResponse.BadImagesError);
        }

        return Convert.FromBase64String(b64);
    }

    private ClientException ToClientException(
        HttpStatusCode statusCode,
        string body,
        string model,
        long chatId)
    {
        log.LogError("OpenRouter {Model} failed: {Status} {Body}", model, (int)statusCode, Truncate(body));

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return new ClientException(chatId, DalleResponse.RateLimit);
        }

        if (body.Contains("content_policy_violation", StringComparison.OrdinalIgnoreCase)
            || body.Contains("moderation", StringComparison.OrdinalIgnoreCase))
        {
            return new ClientException(chatId, DalleResponse.BlockedPromptError);
        }

        return new ClientException(chatId, DalleResponse.BadImagesError);
    }

    private static string Truncate(string body) => body.Length > 500 ? body[..500] : body;

    private sealed class OpenRouterImageRequest
    {
        [JsonPropertyName("model")] public string Model { get; init; } = null!;
        [JsonPropertyName("prompt")] public string Prompt { get; init; } = null!;
        [JsonPropertyName("aspect_ratio")] public string AspectRatio { get; init; } = null!;
        [JsonPropertyName("output_format")] public string OutputFormat { get; init; } = null!;
        [JsonPropertyName("input_references")] public List<OpenRouterInputReference> InputReferences { get; init; } = null!;
    }

    private sealed class OpenRouterInputReference
    {
        [JsonPropertyName("type")] public string Type { get; init; } = null!;
        [JsonPropertyName("image_url")] public OpenRouterImageUrl ImageUrl { get; init; } = null!;
    }

    private sealed class OpenRouterImageUrl
    {
        [JsonPropertyName("url")] public string Url { get; init; } = null!;
    }

    private sealed class OpenRouterImageResponse
    {
        [JsonPropertyName("data")] public List<OpenRouterImageData>? Data { get; init; }
    }

    private sealed class OpenRouterImageData
    {
        [JsonPropertyName("b64_json")] public string? B64Json { get; init; }
    }
}
