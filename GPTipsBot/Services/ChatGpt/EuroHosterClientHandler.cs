using System.Net;
using GPTipsBot.Config;

namespace GPTipsBot.Services
{
    /// <summary>
    /// https://eurohoster.org/
    /// </summary>
    public class EuroHosterClientHandler : HttpClientHandler
    {
        public EuroHosterClientHandler()
        {
            Proxy = new WebProxy(AppConfig.ProxyIp, int.Parse(AppConfig.ProxyPort)) 
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
