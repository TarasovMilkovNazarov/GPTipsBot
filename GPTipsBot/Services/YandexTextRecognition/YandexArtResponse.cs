namespace GPTipsBot.Services.YandexTextRecognition;

// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
public class YandexArtResponse
{
    public string id { get; set; }
    public string description { get; set; }
    public object createdAt { get; set; }
    public string createdBy { get; set; }
    public object modifiedAt { get; set; }
    public bool done { get; set; }
    public object metadata { get; set; }
    public ImageResponse? response { get; set; }
}

public class ImageResponse
{
    public string image { get; set; }
    public string modelVersion { get; set; }
}