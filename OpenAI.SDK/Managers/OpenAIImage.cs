using OpenAI.Extensions;
using OpenAI.Interfaces;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels.ImageResponseModel;

namespace OpenAI.Managers;

public partial class OpenAIService : IImageService
{
    /// <summary>
    ///     Creates an image given a prompt.
    /// </summary>
    public async Task<ImageCreateResponse> CreateImage(ImageCreateRequest imageCreateModel, CancellationToken cancellationToken = default)
    {
        return await _httpClient.PostAndReadAsAsync<ImageCreateResponse>(_endpointProvider.ImageCreate(), imageCreateModel, cancellationToken);
    }

    /// <summary>
    ///     Creates an edited or extended image given an original image and a prompt.
    /// </summary>
    public async Task<ImageCreateResponse> CreateImageEdit(ImageEditCreateRequest imageEditCreateRequest, CancellationToken cancellationToken = default)
    {
        var multipartContent = new MultipartFormDataContent();
        AddOptionalString(multipartContent, "user", imageEditCreateRequest.User);
        AddOptionalString(multipartContent, "response_format", imageEditCreateRequest.ResponseFormat);
        AddOptionalString(multipartContent, "output_format", imageEditCreateRequest.OutputFormat);
        AddOptionalString(multipartContent, "size", imageEditCreateRequest.Size);
        AddOptionalString(multipartContent, "quality", imageEditCreateRequest.Quality);
        AddOptionalString(multipartContent, "model", imageEditCreateRequest.Model);

        if (imageEditCreateRequest.N != null)
        {
            multipartContent.Add(new StringContent(imageEditCreateRequest.N.ToString()!), "n");
        }

        if (imageEditCreateRequest.Mask != null)
        {
            multipartContent.Add(
                new ByteArrayContent(imageEditCreateRequest.Mask),
                "mask",
                imageEditCreateRequest.MaskName ?? "mask.png");
        }

        multipartContent.Add(new StringContent(imageEditCreateRequest.Prompt), "prompt");
        multipartContent.Add(
            new ByteArrayContent(imageEditCreateRequest.Image),
            "image",
            string.IsNullOrWhiteSpace(imageEditCreateRequest.ImageName) ? "image.png" : imageEditCreateRequest.ImageName);

        return await _httpClient.PostFileAndReadAsAsync<ImageCreateResponse>(
            _endpointProvider.ImageEditCreate(),
            multipartContent,
            cancellationToken);
    }

    /// <summary>
    ///     Creates a variation of a given image.
    /// </summary>
    public async Task<ImageCreateResponse> CreateImageVariation(ImageVariationCreateRequest imageEditCreateRequest, CancellationToken cancellationToken = default)
    {
        var multipartContent = new MultipartFormDataContent();
        AddOptionalString(multipartContent, "user", imageEditCreateRequest.User);
        AddOptionalString(multipartContent, "response_format", imageEditCreateRequest.ResponseFormat);
        AddOptionalString(multipartContent, "size", imageEditCreateRequest.Size);
        AddOptionalString(multipartContent, "model", imageEditCreateRequest.Model);

        if (imageEditCreateRequest.N != null)
        {
            multipartContent.Add(new StringContent(imageEditCreateRequest.N.ToString()!), "n");
        }

        multipartContent.Add(
            new ByteArrayContent(imageEditCreateRequest.Image),
            "image",
            string.IsNullOrWhiteSpace(imageEditCreateRequest.ImageName) ? "image.png" : imageEditCreateRequest.ImageName);

        return await _httpClient.PostFileAndReadAsAsync<ImageCreateResponse>(
            _endpointProvider.ImageVariationCreate(),
            multipartContent,
            cancellationToken);
    }

    private static void AddOptionalString(MultipartFormDataContent content, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            content.Add(new StringContent(value), name);
        }
    }
}
