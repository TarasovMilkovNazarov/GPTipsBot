using System.Text.Json.Serialization;

public class GenerateResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; }
}