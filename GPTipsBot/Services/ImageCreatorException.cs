using RestSharp;

namespace GPTipsBot.Services;
public class ImageCreatorException : Exception
{
    public RestResponse? Response { get; }

    public ImageCreatorException(Exception ex, RestResponse? response) 
        : base(null, ex)
    {
        Response = response;
    }
}