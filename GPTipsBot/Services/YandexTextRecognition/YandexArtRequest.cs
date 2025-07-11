namespace GPTipsBot.Services.YandexTextRecognition;

// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
public class AspectRatio
{
    public string WidthRatio { get; set; }
    public string HeightRatio { get; set; }
}

public class GenerationOptions
{
    public string Seed { get; set; }
    public AspectRatio AspectRatio { get; set; }
}

public class Message
{
    public string Weight { get; set; }
    public string Text { get; set; }
}

public class YandexArtRequest
{
    public string ModelUri { get; set; }
    public GenerationOptions GenerationOptions { get; set; }
    public List<Message> Messages { get; set; }
}