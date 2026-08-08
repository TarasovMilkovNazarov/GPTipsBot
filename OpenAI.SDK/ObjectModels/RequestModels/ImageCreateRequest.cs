using System.Text.Json.Serialization;
using OpenAI.ObjectModels.SharedModels;

namespace OpenAI.ObjectModels.RequestModels;

/// <summary>
///     Image Create Request Model
/// </summary>
public record ImageCreateRequest : SharedImageRequestBaseModel, IOpenAiModels.IUser
{
    public ImageCreateRequest()
    {
    }

    public ImageCreateRequest(string prompt)
    {
        Prompt = prompt;
    }

    /// <summary>
    ///     A text description of the desired image(s).
    /// </summary>
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }

    /// <summary>
    ///     The style of the generated images. Must be one of vivid or natural.
    ///     Only supported for dall-e-3.
    /// </summary>
    [JsonPropertyName("style")]
    public string? Style { get; set; }
}
