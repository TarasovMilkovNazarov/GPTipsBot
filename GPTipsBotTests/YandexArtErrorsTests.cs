using System.Net;
using GPTipsBot.Services.YandexCloud;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class YandexArtErrorsTests
{
    private const string RejectionBody =
        """
        {"error":"Я не могу сгенерировать это изображение. Давайте попробуем другую тему.","code":3,"message":"Я не могу сгенерировать это изображение. Давайте попробуем другую тему.","details":[]}
        """;

    [Test]
    public void IsContentRejection_Code3BadRequest_ReturnsTrue()
    {
        Assert.That(
            YandexArtErrors.IsContentRejection(HttpStatusCode.BadRequest, RejectionBody),
            Is.True);
    }

    [Test]
    public void IsContentRejection_OtherBadRequest_ReturnsFalse()
    {
        const string body = """{"error":"invalid request","code":1,"message":"bad aspect ratio"}""";

        Assert.That(
            YandexArtErrors.IsContentRejection(HttpStatusCode.BadRequest, body),
            Is.False);
    }

    [Test]
    public void IsContentRejection_Code3InternalServerError_ReturnsFalse()
    {
        Assert.That(
            YandexArtErrors.IsContentRejection(HttpStatusCode.InternalServerError, RejectionBody),
            Is.False);
    }

    [Test]
    public void TryParse_RejectionBody_ParsesCodeAndMessage()
    {
        Assert.That(YandexArtErrors.TryParse(RejectionBody, out var error), Is.True);
        Assert.That(error!.Code, Is.EqualTo(3));
        Assert.That(error.Message, Does.Contain("не могу сгенерировать"));
    }
}
