namespace GPTipsBot.Services.YandexTextRecognition;

// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
public class YandexArtResponse
{
    public string Id { get; set; }
    public string Description { get; set; }
    public object CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public object ModifiedAt { get; set; }
    public bool Done { get; set; }
    public object Metadata { get; set; }
    public ImageResponse? Response { get; set; }
}

public class ImageResponse
{
    public string Image { get; set; }
    public string ModelVersion { get; set; }
}