using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GPTipsBot.Services.YandexCloud;

/// <summary>
/// OpenAI-compatible Images API (POST /v1/images/generations): one call, image in the response.
/// </summary>
public class YandexImagesRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }

    [JsonPropertyName("size")]
    public string Size { get; set; }
}

public class YandexImagesResponse
{
    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("data")]
    public List<YandexImagesData>? Data { get; set; }
}

public class YandexImagesData
{
    [JsonPropertyName("b64_json")]
    public string? ImageBase64 { get; set; }
}

public sealed class YandexImagesErrorResponse
{
    [JsonPropertyName("error")]
    public YandexImagesError? Error { get; set; }
}

public sealed class YandexImagesError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public static class YandexImagesErrors
{
    /// <summary>Images API: prompt blocked or unsupported topic.</summary>
    public const string ContentRejectedType = "invalid_request_error";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static bool IsContentRejection(HttpStatusCode statusCode, string responseBody) =>
        statusCode == HttpStatusCode.BadRequest &&
        TryParse(responseBody, out var error) &&
        error?.Error?.Type == ContentRejectedType;

    public static bool TryParse(string responseBody, out YandexImagesErrorResponse? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return false;
        }

        try
        {
            error = JsonSerializer.Deserialize<YandexImagesErrorResponse>(responseBody, JsonOptions);
            return error is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
