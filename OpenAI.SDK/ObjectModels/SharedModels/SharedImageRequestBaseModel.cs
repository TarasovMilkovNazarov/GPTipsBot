using System.Text.Json.Serialization;

namespace OpenAI.ObjectModels.SharedModels;

public record SharedImageRequestBaseModel
{
    /// <summary>
    ///     The number of images to generate. Must be between 1 and 10.
    ///     For dall-e-3 model, only n=1 is supported.
    /// </summary>
    [JsonPropertyName("n")]
    public int? N { get; set; }

    /// <summary>
    ///     The size of the generated images.
    /// </summary>
    [JsonPropertyName("size")]
    public string? Size { get; set; }

    /// <summary>
    ///     The format in which the generated images are returned. Must be one of url or b64_json.
    ///     Prefer <see cref="OutputFormat"/> for GPT Image models.
    /// </summary>
    [JsonPropertyName("response_format")]
    public string? ResponseFormat { get; set; }

    /// <summary>
    ///     Output file format for GPT Image models: png, jpeg, or webp.
    /// </summary>
    [JsonPropertyName("output_format")]
    public string? OutputFormat { get; set; }

    /// <summary>
    ///     Image quality. dall-e-3: standard/hd. GPT Image: low/medium/high/auto.
    /// </summary>
    [JsonPropertyName("quality")]
    public string? Quality { get; set; }

    /// <summary>
    ///     A unique identifier representing your end-user, which will help OpenAI to monitor and detect abuse.
    /// </summary>
    [JsonPropertyName("user")]
    public string? User { get; set; }

    /// <summary>
    ///     The model to use for image generation (e.g. dall-e-2, dall-e-3, gpt-image-2).
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }
}
