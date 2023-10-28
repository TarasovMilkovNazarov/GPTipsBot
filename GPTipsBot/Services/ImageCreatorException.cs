using RestSharp;

namespace GPTipsBot.Services;
public class ImageCreatorException : Exception
{
    public RestRequest Request { get; }

    public ImageCreatorException(Exception ex, RestRequest request) 
        : base(null, ex)
    {
        Request = request;
    }
}