using System.Text.Json.Serialization;
using OpenAI.ObjectModels.SharedModels;

namespace OpenAI.ObjectModels.RequestModels;

public record ImageEditCreateRequest : SharedImageRequestBaseModel
{
    /// <summary>
    ///     The image to edit. Must be a valid PNG file, less than 4MB, and square.
    /// </summary>
    public byte[] Image { get; set; }

    /// <summary>
    ///     Image file name
    /// </summary>
    public string ImageName { get; set; }

    /// <summary>
    ///     Extra reference images (gpt-image models only). When set, all images are sent as <c>image[]</c>.
    /// </summary>
    public List<ImageEditInput>? AdditionalImages { get; set; }

    /// <summary>
    ///     An additional image whose fully transparent areas (e.g. where alpha is zero) indicate where image should be edited.
    ///     Must be a valid PNG file, less than 4MB, and have the same dimensions as image.
    /// </summary>
    public byte[]? Mask { get; set; }

    /// <summary>
    ///     Mask file name
    /// </summary>
    public string? MaskName { get; set; }

    /// <summary>
    ///     A text description of the desired image(s). The maximum length is 1000 characters.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }
}

public class ImageEditInput
{
    public ImageEditInput(byte[] image, string imageName)
    {
        Image = image;
        ImageName = imageName;
    }

    public byte[] Image { get; }

    public string ImageName { get; }
}