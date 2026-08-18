using System.Text.Encodings.Web;
using System.Text.Json;
using GPTipsBot.Services.YandexPhotoAnimator;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class AliceGenerateRequestTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [Test]
    public void EditImageGenerateRequest_SerializesExactBrowserShape()
    {
        var json = JsonSerializer.Serialize(new EditImageGenerateRequest
        {
            ImageCount = 1,
            Prompt = "радуга",
            Url = "https://yaart-wow-alice-tmp.s3.yandex.net/6db405029b3011f198968a8f80477d4c",
            IsTemplate = "0"
        }, Options);

        Assert.That(json, Is.EqualTo(
            """{"imageCount":1,"prompt":"радуга","url":"https://yaart-wow-alice-tmp.s3.yandex.net/6db405029b3011f198968a8f80477d4c","is_template":"0"}"""));
    }

    [Test]
    public void CombineImagesGenerateRequest_SerializesExactBrowserShape()
    {
        var json = JsonSerializer.Serialize(new CombineImagesGenerateRequest
        {
            Prompt = "рядом",
            Urls = new[]
            {
                "https://example.com/a.jpg",
                "https://example.com/b.jpg"
            },
            ImageCount = 1,
            Edit = false,
            IsTemplate = "0"
        }, Options);

        Assert.That(json, Is.EqualTo(
            """{"prompt":"рядом","urls":["https://example.com/a.jpg","https://example.com/b.jpg"],"imageCount":1,"edit":false,"is_template":"0"}"""));
    }

    [Test]
    public void GetVideoRequest_SerializesGenerationId()
    {
        var json = JsonSerializer.Serialize(new GetVideoRequest
        {
            GenerationId = "b9342d8c9b3011f19daee21c62c85c43"
        }, Options);

        Assert.That(json, Is.EqualTo("""{"generationID":"b9342d8c9b3011f19daee21c62c85c43"}"""));
    }

    [Test]
    public void EditImageGenerateRequest_IsTemplateIsStringNotNumber()
    {
        var json = JsonSerializer.Serialize(new EditImageGenerateRequest
        {
            Prompt = "x",
            Url = "https://example.com/a.jpg"
        }, Options);

        Assert.That(json, Does.Contain("\"is_template\":\"0\""));
        Assert.That(json, Does.Not.Contain("\"is_template\":0"));
        Assert.That(json, Does.Contain("\"imageCount\":1"));
    }

    [Test]
    public void EditImageGenerateRequest_SerializesWhenBoxedAsObject()
    {
        object body = new EditImageGenerateRequest
        {
            ImageCount = 1,
            Prompt = "радуга",
            Url = "https://example.com/a.jpg",
            IsTemplate = "0"
        };

        var json = JsonSerializer.Serialize(body, Options);

        Assert.That(json, Is.EqualTo(
            """{"imageCount":1,"prompt":"радуга","url":"https://example.com/a.jpg","is_template":"0"}"""));
    }
}
