using System.Text.Json.Serialization;

namespace GPTipsBot.Services.YandexCloud;

public sealed class YandexArtErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("code")]
    public int? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
