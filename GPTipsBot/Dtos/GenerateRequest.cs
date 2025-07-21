using System.Text.Json.Serialization;

namespace GPTipsBot.Dtos;

public class GenerateRequest
{
    public string Model { get; set; }
    public string Action { get; set; }
    public string Prompt { get; set; }
    [JsonPropertyName("aspect_ration")]
    public string AspectRation { get; set; }
}