using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Localization
{
    public class LocalizationManager
    {
        private static readonly CultureInfo[] cultureInfos = new CultureInfo[] { new CultureInfo("en"), new CultureInfo("ru") };
        public static CultureInfo[] SupportedCultures = cultureInfos;

        public static CultureInfo GetCulture(string? langCode)
        {
            var userCulture = new CultureInfo(langCode ?? "en");

            return SupportedCultures.ToList().Contains(userCulture) ? userCulture : SupportedCultures[0];
        }
    }
}
