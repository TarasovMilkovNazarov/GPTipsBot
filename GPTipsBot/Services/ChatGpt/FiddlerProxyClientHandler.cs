using System.Net;
using System.Net.Http.Headers;

namespace GPTipsBot.Services
{
    public class FiddlerProxyClientHandler : HttpClientHandler
    {
        public FiddlerProxyClientHandler()
        {
            Proxy = new WebProxy("http://localhost", 8888);
            UseProxy = true;
            ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
        }
    }
}
