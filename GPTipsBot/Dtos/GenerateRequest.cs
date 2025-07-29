using System.Text.Json.Serialization;

namespace GPTipsBot.Dtos;

public class GenerateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; }
    [JsonPropertyName("action")]
    public string Action { get; set; }
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }
    [JsonPropertyName("aspect_ratio")]
    public string AspectRatio { get; set; }

    [JsonPropertyName("response_format")] public string ResponseFormat { get; set; } = "b64_json";

    [JsonPropertyName("image_url")] public string Image { get; set; } = "data:image/jpeg;base64,{0}";
}