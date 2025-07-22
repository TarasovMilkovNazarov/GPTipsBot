using System.Text.Json.Serialization;

public class GenerateResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; }
    [JsonPropertyName("url")]
    public string?  Url { get; set; }

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}