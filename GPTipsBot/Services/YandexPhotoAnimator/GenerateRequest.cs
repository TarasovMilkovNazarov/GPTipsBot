using System.Text.Json.Serialization;

namespace GPTipsBot.Services.YandexPhotoAnimator;

public class GenerateRequest
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}