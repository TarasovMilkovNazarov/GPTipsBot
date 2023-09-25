using System.Collections.Specialized;
using System.Web;

namespace GPTipsBot.Services
{
    public static class TelegramService
    {
        public static NameValueCollection ParseDeeplink(string deepLink){
            Uri uri = new Uri(deepLink);
            string query = uri.Query;

            NameValueCollection queryParams = HttpUtility.ParseQueryString(query);

            return queryParams;
        }

        public static string? GetSource(string? text){
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return text.StartsWith("/start") ? text.Substring("/start".Length).Trim() : null;
        }
    }
}
