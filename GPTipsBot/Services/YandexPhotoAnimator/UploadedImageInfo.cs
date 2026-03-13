using System.Text.Json.Serialization;

namespace GPTipsBot.Services.YandexPhotoAnimator;

public class UploadedImageInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("userID")]
    public string UserId { get; set; }

    [JsonPropertyName("imageURL")]
    public string ImageUrl { get; set; }
}