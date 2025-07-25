using RestSharp;

namespace GPTipsBot.Services;
public class ImageCreatorException(Exception ex, RestResponse? response) : Exception(null, ex)
{
    public RestResponse? Response { get; } = response;
}