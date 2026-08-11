namespace GPTipsBot.Dtos;

public class StatisticsDto
{
    public int ImagesCount { get; set; }
    public int RecognitionsCount { get; set; }
    public int GptResponses { get; set; }
    public int AnimatedPhotosCount { get; set; }
    public int PromptFromImageCount { get; set; }
}