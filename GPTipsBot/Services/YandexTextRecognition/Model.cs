namespace GPTipsBot.Services.YandexTextRecognition;
public class Block
{
    public BoundingBox boundingBox { get; set; }
    public List<Line> lines { get; set; }
    public List<Language> languages { get; set; }
    public List<TextSegment> textSegments { get; set; }
}

public class BoundingBox
{
    public List<Vertex> vertices { get; set; }
}

public class Language
{
    public string languageCode { get; set; }
}

public class Line
{
    public BoundingBox boundingBox { get; set; }
    public string text { get; set; }
    public List<Word> words { get; set; }
    public List<TextSegment> textSegments { get; set; }
    public string orientation { get; set; }
}

public class Result
{
    public TextAnnotation textAnnotation { get; set; }
    public string page { get; set; }
}

public class Root
{
    public Result result { get; set; }
}

public class TextAnnotation
{
    public string width { get; set; }
    public string height { get; set; }
    public List<Block> blocks { get; set; }
    public List<object> entities { get; set; }
    public List<object> tables { get; set; }
    public string fullText { get; set; }
    public string rotate { get; set; }
}

public class TextSegment
{
    public string startIndex { get; set; }
    public string length { get; set; }
}

public class Vertex
{
    public string x { get; set; }
    public string y { get; set; }
}

public class Word
{
    public BoundingBox boundingBox { get; set; }
    public string text { get; set; }
    public string entityIndex { get; set; }
    public List<TextSegment> textSegments { get; set; }
}

