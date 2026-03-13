namespace GPTipsBot.Services.YandexPhotoAnimator;

public class VideoGenerationResponse
{
    public VideoGeneration VideoGeneration { get; set; }
}

public class VideoGeneration
{
    public string Id { get; set; }
    public string Status { get; set; }
    public string Prompt { get; set; }
    public string FirstFrameURL { get; set; }
    public int RemainingTimeSec { get; set; }
    public string VideoID { get; set; }
    public string VideoURL { get; set; }
    public string PostID { get; set; }
    public int PreviewWidth { get; set; }
    public int PreviewHeight { get; set; }
}