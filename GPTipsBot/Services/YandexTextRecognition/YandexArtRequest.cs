namespace GPTipsBot.Services.YandexTextRecognition;

// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
public class AspectRatio
{
    public string widthRatio { get; set; }
    public string heightRatio { get; set; }
}

public class GenerationOptions
{
    public string seed { get; set; }
    public AspectRatio aspectRatio { get; set; }
}

public class Message
{
    public string weight { get; set; }
    public string text { get; set; }
}

public class YandexArtRequest
{
    public string modelUri { get; set; }
    public GenerationOptions generationOptions { get; set; }
    public List<Message> messages { get; set; }
}