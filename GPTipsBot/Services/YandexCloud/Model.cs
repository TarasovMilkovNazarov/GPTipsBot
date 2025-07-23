namespace GPTipsBot.Services.YandexCloud;
public class Block
{
    public BoundingBox BoundingBox { get; set; }
    public List<Line> Lines { get; set; }
    public List<Language> Languages { get; set; }
    public List<TextSegment> TextSegments { get; set; }
}

public class BoundingBox
{
    public List<Vertex> Vertices { get; set; }
}

public class Language
{
    public string LanguageCode { get; set; }
}

public class Line
{
    public BoundingBox BoundingBox { get; set; }
    public string Text { get; set; }
    public List<Word> Words { get; set; }
    public List<TextSegment> TextSegments { get; set; }
    public string Orientation { get; set; }
}

public class Result
{
    public TextAnnotation TextAnnotation { get; set; }
    public string Page { get; set; }
}

public class Root
{
    public Result Result { get; set; }
}

public class TextAnnotation
{
    public string Width { get; set; }
    public string Height { get; set; }
    public List<Block> Blocks { get; set; }
    public List<object> Entities { get; set; }
    public List<object> Tables { get; set; }
    public string FullText { get; set; }
    public string Rotate { get; set; }
}

public class TextSegment
{
    public string StartIndex { get; set; }
    public string Length { get; set; }
}

public class Vertex
{
    public string X { get; set; }
    public string Y { get; set; }
}

public class Word
{
    public BoundingBox BoundingBox { get; set; }
    public string Text { get; set; }
    public string EntityIndex { get; set; }
    public List<TextSegment> TextSegments { get; set; }
}

