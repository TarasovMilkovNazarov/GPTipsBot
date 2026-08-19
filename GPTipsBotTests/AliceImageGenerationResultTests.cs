using GPTipsBot.Services.YandexPhotoAnimator;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class AliceImageGenerationResultTests
{
    [Test]
    public void FromJson_EditingGeneration_ReadsIdAndImageUrl()
    {
        const string json = """
            {
              "editingGeneration": {
                "id": "gen-1",
                "status": "SUCCESS",
                "remainingTimeSec": 3,
                "imageURL": "https://example.com/edit.jpg"
              }
            }
            """;

        var result = AliceImageGenerationResult.FromJson(json, "editingGeneration");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("gen-1"));
        Assert.That(result.Status, Is.EqualTo("SUCCESS"));
        Assert.That(result.RemainingTimeSec, Is.EqualTo(3));
        Assert.That(result.ImageUrl, Is.EqualTo("https://example.com/edit.jpg"));
    }

    [Test]
    public void FromJson_CombiningGeneration_ReadsResultUrl()
    {
        const string json = """
            {
              "imageCombiningGeneration": {
                "Id": "gen-2",
                "resultURL": "https://example.com/combine.jpg"
              }
            }
            """;

        var result = AliceImageGenerationResult.FromJson(json, "imageCombiningGeneration");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("gen-2"));
        Assert.That(result.ImageUrl, Is.EqualTo("https://example.com/combine.jpg"));
    }

    [Test]
    public void FromJson_EditingGeneration_ReadsNestedResultsImageUrl()
    {
        const string json = """
            {
              "editingGeneration": {
                "id": "27f54b139b3511f1949622e14fa19f98",
                "status": "pending",
                "prompt": "преврати в аниме",
                "originalURL": "https://yaart-images.s3.yandex.net/webalice/original_27f54b139b3511f1949622e14fa19f98",
                "estimateTimeSec": 46,
                "remainingTimeSec": 0,
                "results": {
                  "postID": "27f54b139b3511f1949622e14fa19f98",
                  "images": [
                    {
                      "id": "editing_result_27f54b139b3511f1949622e14fa19f98:1",
                      "width": 0,
                      "height": 0,
                      "imageURL": "https://yaart-images.s3.yandex.net/webalice/editing_result_27f54b139b3511f1949622e14fa19f98:1"
                    }
                  ]
                },
                "imageCount": 1
              }
            }
            """;

        var result = AliceImageGenerationResult.FromJson(json, "editingGeneration");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("27f54b139b3511f1949622e14fa19f98"));
        Assert.That(result.Status, Is.EqualTo("pending"));
        Assert.That(result.RemainingTimeSec, Is.EqualTo(0));
        Assert.That(
            result.ImageUrl,
            Is.EqualTo("https://yaart-images.s3.yandex.net/webalice/editing_result_27f54b139b3511f1949622e14fa19f98:1"));
    }

    [Test]
    public void FromJson_CombiningGeneration_ReadsTopLevelImagesArrayUrl()
    {
        const string json = """
            {
              "imageCombiningGeneration": {
                "id": "cc7639589b9711f1b5d772428e827e6c",
                "kind": "combining",
                "status": "pending",
                "remainingTimeSec": 0,
                "estimateTimeSec": 58,
                "images": [
                  {
                    "id": "cc7639589b9711f1b5d772428e827e6c_1",
                    "imageURL": "https://yaart-images.s3.yandex.net/webalice/cc7639589b9711f1b5d772428e827e6c_1"
                  }
                ]
              }
            }
            """;

        var result = AliceImageGenerationResult.FromJson(json, "imageCombiningGeneration");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("cc7639589b9711f1b5d772428e827e6c"));
        Assert.That(result.Status, Is.EqualTo("pending"));
        Assert.That(result.RemainingTimeSec, Is.EqualTo(0));
        Assert.That(result.EstimateTimeSec, Is.EqualTo(58));
        Assert.That(
            result.ImageUrl,
            Is.EqualTo("https://yaart-images.s3.yandex.net/webalice/cc7639589b9711f1b5d772428e827e6c_1"));
    }

    [Test]
    public void FromJson_InProgressSourceUrl_IsNotTreatedAsResult()
    {
        const string json = """
            {
              "imageCombiningGeneration": {
                "id": "gen-3",
                "status": "IN_PROGRESS",
                "remainingTimeSec": 12,
                "url": "https://example.com/source.jpg"
              }
            }
            """;

        var result = AliceImageGenerationResult.FromJson(json, "imageCombiningGeneration");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("gen-3"));
        Assert.That(result.ImageUrl, Is.Null);
        Assert.That(result.RemainingTimeSec, Is.EqualTo(12));
    }
}
