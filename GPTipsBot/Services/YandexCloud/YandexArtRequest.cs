using System.Text.Json.Serialization;

namespace GPTipsBot.Services.YandexCloud;

// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
public class AspectRatio
{
    [JsonPropertyName("widthRatio")]
    public string WidthRatio { get; set; }
    [JsonPropertyName("heightRatio")]
    public string HeightRatio { get; set; }
}

public class GenerationOptions
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; }
    [JsonPropertyName("aspectRatio")]
    public AspectRatio AspectRatio { get; set; }
}

public class Message
{
    [JsonPropertyName("weight")]
    public string Weight { get; set; }
    [JsonPropertyName("text")]
    public string Text { get; set; }
}

public class YandexArtRequest
{
    [JsonPropertyName("modelUri")]
    public string ModelUri { get; set; }
    [JsonPropertyName("generationOptions")]
    public GenerationOptions GenerationOptions { get; set; }
    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; }
}