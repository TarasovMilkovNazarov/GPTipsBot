using System.Collections.Specialized;
using System.Web;
using GPTipsBot.Services;

namespace GPTipsBot.Services
{
    public static class TelegramService
    {
        public static NameValueCollection ParseDeeplink(string deepLink){
            var uri = new Uri(deepLink);
            var query = uri.Query;

            var queryParams = HttpUtility.ParseQueryString(query);

            return queryParams;
        }

        public static string? GetSource(string? text){
            if (string.IsNullOrEmpty(text) ||
                !text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var rest = text["/start".Length..].TrimStart();
            if (rest.StartsWith('@'))
            {
                var space = rest.IndexOf(' ');
                rest = space < 0 ? string.Empty : rest[(space + 1)..].Trim();
            }
            else
            {
                rest = rest.Trim();
            }

            if (string.IsNullOrEmpty(rest))
            {
                return null;
            }

            // Account-link deep links are not referral sources.
            if (rest.StartsWith(AccountLinkTokenService.Prefix, StringComparison.Ordinal))
            {
                return null;
            }

            return rest;
        }
    }
}
