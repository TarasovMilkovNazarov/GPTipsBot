namespace GPTipsBot.Config;

/// <summary>
/// Selectable chat models and Stars pricing aligned with OpenAI API $/1M rates.
/// Free quota applies only to the default model.
/// See https://developers.openai.com/api/docs/models/all
/// </summary>
public static class GptModelCatalog
{
    public const string DefaultModelId = "gpt-4o-mini";

    public static GptModelOption Default => GetRequired(DefaultModelId);

    /// <summary>
    /// Popular Chat Completions models, cheapest → strongest.
    /// Stars ≈ relative blended cost vs gpt-4o-mini (typical short chat turn).
    /// </summary>
    public static IReadOnlyList<GptModelOption> All { get; } =
    [
        // $0.15 / $0.60 — baseline
        new(
            Id: DefaultModelId,
            DisplayName: "GPT-4o mini",
            StarsCost: PaymentConfig.Gpt,
            AllowFreeQuota: true,
            Emoji: "⚡"),
        // $0.25 / $2.00 — ~3× baseline
        new(
            Id: "gpt-5-mini",
            DisplayName: "GPT-5 mini",
            StarsCost: PaymentConfig.Gpt5Mini,
            AllowFreeQuota: false,
            Emoji: "🚀"),
        // $2.00 / $8.00 — smart non-reasoning, ~10×
        new(
            Id: "gpt-4.1",
            DisplayName: "GPT-4.1",
            StarsCost: PaymentConfig.Gpt41,
            AllowFreeQuota: false,
            Emoji: "🧠"),
        // $1.25 / $10.00 — flagship, ~12× (output-heavy)
        new(
            Id: "gpt-5",
            DisplayName: "GPT-5",
            StarsCost: PaymentConfig.Gpt5,
            AllowFreeQuota: false,
            Emoji: "✨"),
        // $2.00 / $8.00 + reasoning tokens — ~20×
        new(
            Id: "o3",
            DisplayName: "o3",
            StarsCost: PaymentConfig.GptReasoning,
            AllowFreeQuota: false,
            Emoji: "🧮"),
    ];

    public static GptModelOption? Find(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        var exact = All.FirstOrDefault(m =>
            string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            return exact;
        }

        // Migrate removed picker options to closest current models.
        return modelId.ToLowerInvariant() switch
        {
            "gpt-4o" => Find("gpt-4.1"),
            "o4-mini" => Find("o3"),
            _ => null,
        };
    }

    public static GptModelOption GetRequired(string modelId) =>
        Find(modelId) ?? throw new ArgumentOutOfRangeException(nameof(modelId), modelId, "Unknown GPT model");

    public static GptModelOption Resolve(string? preferredModelId) =>
        Find(preferredModelId) ?? Default;
}

public sealed record GptModelOption(
    string Id,
    string DisplayName,
    double StarsCost,
    bool AllowFreeQuota,
    string Emoji)
{
    public string FormatButtonLabel(bool selected)
    {
        var check = selected ? "✅ " : "";
        return $"{check}{Emoji} {DisplayName} · {StarsCost:0.##}⭐";
    }
}
