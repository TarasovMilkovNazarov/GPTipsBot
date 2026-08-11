using GPTipsBot.Services;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class NaturalLanguageToolRouterTests
{
    [TestCase("Create an image of a cat in space", MediaToolIntent.GenerateImage, "a cat in space")]
    [TestCase("generate a picture of an Ethiopian farmer", MediaToolIntent.GenerateImage, "an Ethiopian farmer")]
    [TestCase("нарисуй картинку кота в космосе", MediaToolIntent.GenerateImage, "кота в космосе")]
    [TestCase("сгенерируй изображение: закат над морем", MediaToolIntent.GenerateImage, "закат над морем")]
    [TestCase("Crea una imagen de un perro", MediaToolIntent.GenerateImage, "un perro")]
    [TestCase("image a cat in space", MediaToolIntent.GenerateImage, "a cat in space")]
    [TestCase("/image a cat in space", MediaToolIntent.GenerateImage, "a cat in space")]
    public void TryMatch_GenerateWithPrompt(string text, MediaToolIntent intent, string prompt)
    {
        var route = NaturalLanguageToolRouter.TryMatch(text, hasPhoto: false);
        Assert.That(route.Intent, Is.EqualTo(intent));
        Assert.That(route.Prompt, Is.EqualTo(prompt));
    }

    [TestCase("создай картинку")]
    [TestCase("Create an image")]
    [TestCase("Necesito crear imágenes en photo")]
    [TestCase("Haz una imagen")]
    public void TryMatch_GenerateWithoutPrompt(string text)
    {
        var route = NaturalLanguageToolRouter.TryMatch(text, hasPhoto: false);
        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.GenerateImage));
        Assert.That(route.Prompt, Is.Null.Or.Empty);
    }

    [TestCase("Puedes crear imágenes?")]
    [TestCase("Can you generate images?")]
    [TestCase("Ты умеешь генерировать картинки?")]
    [TestCase("да хз, фотки умеешь генерировать?")]
    public void TryMatch_CapabilityAsk_OpensImagesMenu(string text)
    {
        var route = NaturalLanguageToolRouter.TryMatch(text, hasPhoto: false);
        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.ImagesMenu));
    }

    [TestCase("Hi")]
    [TestCase("hello")]
    [TestCase("Hola")]
    [TestCase("привет")]
    [TestCase("Как дела")]
    [TestCase("?")]
    [TestCase("что умеешь")]
    [TestCase("What can you do?")]
    [TestCase("Que puedes hacer")]
    [TestCase("кто ты")]
    [TestCase("Me puedes recordar tus funciones")]
    [TestCase("как пользоваться")]
    public void TryMatch_Onboarding(string text)
    {
        var route = NaturalLanguageToolRouter.TryMatch(text, hasPhoto: false);
        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.Onboarding));
    }

    [TestCase("распознай текст")]
    [TestCase("распознай текст с фото")]
    [TestCase("extract text from the image")]
    [TestCase("OCR")]
    public void TryMatch_Ocr(string text)
    {
        var route = NaturalLanguageToolRouter.TryMatch(text, hasPhoto: false);
        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.RecognizeText));
    }

    [TestCase("что на фото")]
    [TestCase("опиши картинку")]
    [TestCase("what's in the photo")]
    [TestCase("describe this image")]
    [TestCase("промпт по фото")]
    [TestCase("create a prompt from this photo")]
    public void TryMatch_PromptFromImage(string text)
    {
        var route = NaturalLanguageToolRouter.TryMatch(text, hasPhoto: false);
        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.PromptFromImage));
    }

    [Test]
    public void TryMatch_BarePhoto_OpensImagesMenu()
    {
        var route = NaturalLanguageToolRouter.TryMatch(null, hasPhoto: true);
        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.ImagesMenu));
    }

    [TestCase("напиши пост про маркетинг")]
    [TestCase("переведи на английский")]
    [TestCase("что такое сны")]
    public void TryMatch_NormalChat_DoesNotRoute(string text)
    {
        var route = NaturalLanguageToolRouter.TryMatch(text, hasPhoto: false);
        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.None));
    }

    [Test]
    public void TryMatch_PhotoWithUnrelatedCaption_DoesNotForceMedia()
    {
        var route = NaturalLanguageToolRouter.TryMatch(
            "Приведи доводы для мужа, мне необходимо поехать отдохнуть",
            hasPhoto: true);
        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.None));
    }
}
