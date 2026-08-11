using System.Text.Json.Serialization;

namespace GPTipsBot.Dtos;

public class YandexRecognitionRequest
{
    [JsonPropertyName("content")]
    public string Content { get; set; }
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "JPEG";
    [JsonPropertyName("model")]
    public string Model { get; set; } = "handwritten";
    [JsonPropertyName("languageCodes")]
    public string[] LanguageCodes { get; set; } = {"ru", "en", "es"};
}