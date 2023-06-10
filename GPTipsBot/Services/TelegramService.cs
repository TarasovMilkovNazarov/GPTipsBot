﻿using System.Collections.Specialized;
using System.Web;

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

        public static string? GetSource(string text){
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return text.Substring("/start".Length).Trim();
        }
    }
}
