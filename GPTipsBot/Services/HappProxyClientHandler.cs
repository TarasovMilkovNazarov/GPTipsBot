using System.Net;
using GPTipsBot.Config;

namespace GPTipsBot.Services;

/// <summary>
/// HTTP-прокси локального клиента Happ (Inbounds → HTTP).
/// </summary>
public class HappProxyClientHandler : HttpClientHandler
{
    public HappProxyClientHandler()
    {
        var proxy = new WebProxy(AppConfig.HappProxyIp, AppConfig.HappProxyPort);
        if (!string.IsNullOrWhiteSpace(AppConfig.HappProxyLogin))
            proxy.Credentials = new NetworkCredential(AppConfig.HappProxyLogin, AppConfig.HappProxyPassword);

        Proxy = proxy;
        UseProxy = true;
        ServerCertificateCustomValidationCallback += (_, _, _, _) => true;
    }
}

public class HappHttpClient() : HttpClient(new HappProxyClientHandler());
