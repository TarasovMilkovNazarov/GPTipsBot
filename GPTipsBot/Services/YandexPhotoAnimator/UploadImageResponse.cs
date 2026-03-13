using System.Text.Json.Serialization;

namespace GPTipsBot.Services.YandexPhotoAnimator;

public class UploadImageResponse
{
    [JsonPropertyName("uploadedImageInfo")]
    public UploadedImageInfo UploadedImageInfo { get; set; }
}