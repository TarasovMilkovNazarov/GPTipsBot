using System.Text.Json;

namespace GPTipsBot.Services.YandexPhotoAnimator;

public class AliceImageGenerationResult
{
    public string? Id { get; set; }
    public string? Status { get; set; }
    public int RemainingTimeSec { get; set; }
    public int EstimateTimeSec { get; set; }
    public string? ImageUrl { get; set; }

    public static AliceImageGenerationResult? FromJson(string json, string generationProperty)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(generationProperty, out var generation) ||
            generation.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var status = ReadString(generation, "status", "Status");
        var imageUrl = ReadResultImageUrl(generation, status);

        return new AliceImageGenerationResult
        {
            Id = ReadString(generation, "id", "Id", "generationID", "generationId"),
            Status = status,
            RemainingTimeSec = ReadInt(generation, "remainingTimeSec", "RemainingTimeSec"),
            EstimateTimeSec = ReadInt(generation, "estimateTimeSec", "EstimateTimeSec"),
            ImageUrl = imageUrl,
        };
    }

    private static string? ReadResultImageUrl(JsonElement generation, string? status)
    {
        var nested = ReadNestedResultImageUrl(generation);
        if (!string.IsNullOrEmpty(nested))
        {
            return nested;
        }

        var imageUrl = ReadString(
            generation,
            "imageURL",
            "imageUrl",
            "ImageURL",
            "resultURL",
            "resultUrl",
            "outputURL",
            "pictureURL");

        if (!string.IsNullOrEmpty(imageUrl) && !IsSameUrl(imageUrl, ReadOriginalUrl(generation)))
        {
            return imageUrl;
        }

        if (IsFinished(status))
        {
            return ReadString(generation, "url", "Url");
        }

        return null;
    }

    private static string? ReadNestedResultImageUrl(JsonElement generation)
    {
        var rootImagesUrl = ReadImagesArrayUrl(generation);
        if (!string.IsNullOrEmpty(rootImagesUrl))
        {
            return rootImagesUrl;
        }

        if (!TryGetProperty(generation, out var results, "results", "Results") ||
            results.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var direct = ReadString(results, "imageURL", "imageUrl", "ImageURL", "resultURL", "resultUrl");
        if (!string.IsNullOrEmpty(direct))
        {
            return direct;
        }

        return ReadImagesArrayUrl(results);
    }

    private static string? ReadImagesArrayUrl(JsonElement container)
    {
        if (!TryGetProperty(container, out var images, "images", "Images") ||
            images.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var image in images.EnumerateArray())
        {
            var url = ReadString(image, "imageURL", "imageUrl", "ImageURL", "url", "Url");
            if (!string.IsNullOrEmpty(url))
            {
                return url;
            }
        }

        return null;
    }

    private static string? ReadOriginalUrl(JsonElement generation)
        => ReadString(generation, "originalURL", "originalUrl", "OriginalURL");

    private static bool IsSameUrl(string left, string? right)
        => !string.IsNullOrEmpty(right) &&
           string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool IsFinished(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase)
            || status.Equals("DONE", StringComparison.OrdinalIgnoreCase)
            || status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase)
            || status.Equals("READY", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static int ReadInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }
}
