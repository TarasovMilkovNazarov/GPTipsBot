using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using OpenAI.ObjectModels.ResponseModels;

namespace GPTipsBotTests;

[TestFixture]
public class MediaIntentClassifierTests
{
    private Mock<IGpt> _gptMock;
    private MediaIntentClassifier _classifier;

    [SetUp]
    public void Setup()
    {
        _gptMock = new Mock<IGpt>();
        _classifier = new MediaIntentClassifier(_gptMock.Object, Mock.Of<ILogger<MediaIntentClassifier>>());
    }

    private void SetupGptResponse(string content)
    {
        var response = new ChatCompletionCreateResponse
        {
            Choices = new() { new() { Message = new("assistant", content) } }
        };

        _gptMock.Setup(m => m.SendOneOffAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .ReturnsAsync(response);
    }

    [Test]
    public async Task TryClassifyAsync_GenerateImageJson_ReturnsRouteWithPrompt()
    {
        SetupGptResponse("""{"intent": "generate_image", "prompt": "жираф в очках"}""");

        var route = await _classifier.TryClassifyAsync("нарисуй жирафа в очках", CancellationToken.None);

        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.GenerateImage));
        Assert.That(route.Prompt, Is.EqualTo("жираф в очках"));
    }

    [Test]
    public async Task TryClassifyAsync_JsonWrappedInMarkdownFence_StillParses()
    {
        SetupGptResponse("```json\n{\"intent\": \"images_menu\", \"prompt\": \"\"}\n```");

        var route = await _classifier.TryClassifyAsync("а фотки покажешь?", CancellationToken.None);

        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.ImagesMenu));
    }

    [TestCase("none")]
    [TestCase("unknown_intent")]
    public async Task TryClassifyAsync_NoneOrUnknownIntent_ReturnsNone(string intent)
    {
        SetupGptResponse($$"""{"intent": "{{intent}}", "prompt": ""}""");

        var route = await _classifier.TryClassifyAsync("как погода", CancellationToken.None);

        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.None));
    }

    [Test]
    public async Task TryClassifyAsync_NotJson_ReturnsNoneAndDoesNotThrow()
    {
        SetupGptResponse("Извините, я не могу рисовать, но могу описать...");

        var route = await _classifier.TryClassifyAsync("нарисуй жирафа", CancellationToken.None);

        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.None));
    }

    [Test]
    public async Task TryClassifyAsync_GptThrows_ReturnsNoneAndDoesNotThrow()
    {
        _gptMock.Setup(m => m.SendOneOffAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var route = await _classifier.TryClassifyAsync("нарисуй жирафа", CancellationToken.None);

        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.None));
    }

    [Test]
    public async Task TryClassifyAsync_RemoveWatermarkJson_ReturnsRoute()
    {
        SetupGptResponse("""{"intent": "remove_watermark", "prompt": ""}""");

        var route = await _classifier.TryClassifyAsync("сможешь очистить логотип с этого фото?", CancellationToken.None);

        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.RemoveWatermark));
        Assert.That(route.Prompt, Is.Null);
    }

    [Test]
    public async Task TryClassifyAsync_StickerPackJson_ReturnsRouteWithPrompt()
    {
        SetupGptResponse("""{"intent": "sticker_pack", "prompt": "рыжий кот"}""");

        var route = await _classifier.TryClassifyAsync("сделай стикерпак рыжего кота", CancellationToken.None);

        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.StickerPack));
        Assert.That(route.Prompt, Is.EqualTo("рыжий кот"));
    }

    [Test]
    public async Task TryClassifyAsync_RecognizeTextIntent_IgnoresPromptField()
    {
        SetupGptResponse("""{"intent": "recognize_text", "prompt": "should be ignored"}""");

        var route = await _classifier.TryClassifyAsync("прочитай текст с фото", CancellationToken.None);

        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.RecognizeText));
        Assert.That(route.Prompt, Is.Null);
    }
}
