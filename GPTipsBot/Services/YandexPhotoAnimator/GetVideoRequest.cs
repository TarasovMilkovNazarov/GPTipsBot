using System.Text.Json.Serialization;

namespace GPTipsBot.Services.YandexPhotoAnimator;

public class GetVideoRequest
{
    [JsonPropertyName("generationID")]
    public string GenerationId { get; set; }
}