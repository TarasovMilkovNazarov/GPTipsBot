using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GPTipsBot.Config;
using GPTipsBot.Exceptions;
using GPTipsBot.Resources;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Services.VseGpt;

/// <summary>
/// VseGPT image-to-image endpoint. OpenAI-compatible URL, but the source image goes in
/// the <c>image_url</c> field of a /images/generations body instead of a multipart upload.
/// </summary>
public class VseGptImageClient(ILogger<VseGptImageClient> log, HttpClient httpClient)
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
            log.LogError("VSEGPT_TOKEN is not configured");
            throw new ClientException(chatId, DalleResponse.BadImagesError);
        }

        var payload = new VseGptImageRequest
        {
            Model = model,
            Prompt = prompt,
            ResponseFormat = "b64_json",
            ImageUrl = $"data:image/jpeg;base64,{imageBase64}",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "images/generations")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppConfig.VseGptToken);
        request.Headers.Add("X-Title", "GPTipsBot watermark removal");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw ToClientException(response.StatusCode, body, model, chatId);
        }

        var parsed = JsonSerializer.Deserialize<VseGptImageResponse>(body, JsonOptions);
        var b64 = parsed?.Data?.FirstOrDefault()?.B64Json;
        if (string.IsNullOrWhiteSpace(b64))
        {
            log.LogError("VseGPT {Model} returned no image", model);
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
        log.LogError("VseGPT {Model} failed: {Status} {Body}", model, (int)statusCode, Truncate(body));

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return new ClientException(chatId, DalleResponse.RateLimit);
        }

        // Moderated models reject the prompt itself with a content policy error.
        if (body.Contains("content_policy_violation", StringComparison.OrdinalIgnoreCase))
        {
            return new ClientException(chatId, DalleResponse.BlockedPromptError);
        }

        return new ClientException(chatId, DalleResponse.BadImagesError);
    }

    private static string Truncate(string body) => body.Length > 500 ? body[..500] : body;

    private sealed class VseGptImageRequest
    {
        [JsonPropertyName("model")] public string Model { get; init; } = null!;
        [JsonPropertyName("prompt")] public string Prompt { get; init; } = null!;
        [JsonPropertyName("response_format")] public string ResponseFormat { get; init; } = null!;
        [JsonPropertyName("image_url")] public string ImageUrl { get; init; } = null!;
    }

    private sealed class VseGptImageResponse
    {
        [JsonPropertyName("data")] public List<VseGptImageData>? Data { get; init; }
    }

    private sealed class VseGptImageData
    {
        [JsonPropertyName("b64_json")] public string? B64Json { get; init; }
    }
}
