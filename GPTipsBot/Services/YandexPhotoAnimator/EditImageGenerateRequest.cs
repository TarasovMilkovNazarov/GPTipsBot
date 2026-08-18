using System.Text.Json.Serialization;

namespace GPTipsBot.Services.YandexPhotoAnimator;

public class EditImageGenerateRequest
{
    [JsonPropertyName("imageCount")]
    [JsonPropertyOrder(0)]
    public int ImageCount { get; set; } = 1;

    [JsonPropertyName("prompt")]
    [JsonPropertyOrder(1)]
    public string Prompt { get; set; } = null!;

    [JsonPropertyName("url")]
    [JsonPropertyOrder(2)]
    public string Url { get; set; } = null!;

    [JsonPropertyName("is_template")]
    [JsonPropertyOrder(3)]
    public string IsTemplate { get; set; } = "0";
}
