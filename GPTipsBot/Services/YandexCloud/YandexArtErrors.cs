using System.Net;
using System.Text.Json;

namespace GPTipsBot.Services.YandexCloud;

public static class YandexArtErrors
{
    /// <summary>YandexART: prompt blocked or unsupported topic.</summary>
    public const int ContentRejectedCode = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static bool IsContentRejection(HttpStatusCode statusCode, string responseBody) =>
        statusCode == HttpStatusCode.BadRequest &&
        TryParse(responseBody, out var error) &&
        error?.Code == ContentRejectedCode;

    public static bool TryParse(string responseBody, out YandexArtErrorResponse? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return false;
        }

        try
        {
            error = JsonSerializer.Deserialize<YandexArtErrorResponse>(responseBody, JsonOptions);
            return error is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
