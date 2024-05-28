using System.Net;
using System.Net.Http.Headers;

namespace GPTipsBot.Services
{
    /// <summary>
    /// https://eurohoster.org/
    /// </summary>
    public class EuroHosterClientHandler : HttpClientHandler
    {
        public EuroHosterClientHandler()
        {
            Proxy = new WebProxy(AppConfig.ProxyIP, int.Parse(AppConfig.ProxyPort)) 
            {
                Credentials = new NetworkCredential(AppConfig.ProxyLogin, AppConfig.ProxyPwd)
            };
            UseProxy = true;
            ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
        }
    }

    public class EuroHosterHttpClient : HttpClient
    {
        public EuroHosterHttpClient() : base(new EuroHosterClientHandler())
        {
        }
    }
}
