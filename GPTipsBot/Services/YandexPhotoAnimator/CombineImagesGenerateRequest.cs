using System.Text.Json.Serialization;

namespace GPTipsBot.Services.YandexPhotoAnimator;

public class CombineImagesGenerateRequest
{
    [JsonPropertyName("prompt")]
    [JsonPropertyOrder(0)]
    public string Prompt { get; set; } = null!;

    [JsonPropertyName("urls")]
    [JsonPropertyOrder(1)]
    public string[] Urls { get; set; } = null!;

    [JsonPropertyName("imageCount")]
    [JsonPropertyOrder(2)]
    public int ImageCount { get; set; } = 1;

    [JsonPropertyName("edit")]
    [JsonPropertyOrder(3)]
    public bool Edit { get; set; }

    [JsonPropertyName("is_template")]
    [JsonPropertyOrder(4)]
    public string IsTemplate { get; set; } = "0";
}
