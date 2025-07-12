using System.Text.Json.Serialization;

namespace GPTipsBot.Services.YandexCloud;

public class YandexArtResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; }
    [JsonPropertyName("CreatedAt")]
    public object CreatedAt { get; set; }
    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; set; }
    [JsonPropertyName("modifiedAt")]
    public object ModifiedAt { get; set; }
    [JsonPropertyName("done")]
    public bool Done { get; set; }
    [JsonPropertyName("metadata")]
    public object Metadata { get; set; }
    [JsonPropertyName("response")]
    public ImageResponse? Response { get; set; }
}

public class ImageResponse
{
    [JsonPropertyName("image")]
    public string Image { get; set; }
    [JsonPropertyName("modelVersion")]
    public string ModelVersion { get; set; }
}