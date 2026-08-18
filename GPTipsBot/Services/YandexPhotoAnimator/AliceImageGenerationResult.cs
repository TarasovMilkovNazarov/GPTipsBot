using System.Text.Json;

namespace GPTipsBot.Services.YandexPhotoAnimator;

public class AliceImageGenerationResult
{
    public string? Id { get; set; }
    public string? Status { get; set; }
    public int RemainingTimeSec { get; set; }
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
        var imageUrl = ReadString(
            generation,
            "imageURL",
            "imageUrl",
            "ImageURL",
            "resultURL",
            "resultUrl",
            "outputURL",
            "pictureURL");

        if (imageUrl == null && IsFinished(status))
        {
            imageUrl = ReadString(generation, "url", "Url");
        }

        return new AliceImageGenerationResult
        {
            Id = ReadString(generation, "id", "Id", "generationID", "generationId"),
            Status = status,
            RemainingTimeSec = ReadInt(generation, "remainingTimeSec", "RemainingTimeSec"),
            ImageUrl = imageUrl,
        };
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
