using System.Globalization;

namespace GPTipsBot.Localization
{
    public class LocalizationManager
    {
        public static readonly CultureInfo[] SupportedCultures;
        public static readonly CultureInfo Ru;
        public static readonly CultureInfo En;
        public static readonly CultureInfo Es;
        public static readonly CultureInfo Fa;
        public static readonly CultureInfo Ar;

        // Страны СНГ
        private static readonly string[] CisCountries;

        static LocalizationManager()
        {
            Ru = new CultureInfo("ru");
            En = new CultureInfo("en");
            Es = new CultureInfo("es");
            Fa = new CultureInfo("fa");
            Ar = new CultureInfo("ar");
            SupportedCultures = new CultureInfo[] { En, Ru, Es, Fa, Ar };
            CisCountries = new string[] {
              "am", // Armenia
              "az", // Azerbaijan
              "by", // Belarus
              "ge", // Georgia
              "kz", // Kazakhstan
              "kg", // Kyrgyzstan
              "md", // Moldova
              "ru", // Russia
              "tj", // Tajikistan
              "tm", // Turkmenistan
              "uz", // Uzbekistan
              "ua", // Ukraine
            };
        }

        public static CultureInfo GetCulture(string? langCode)
        {
            var primary = GetPrimaryLanguageTag(langCode);
            if (primary == null)
            {
                return En;
            }

            if (CisCountries.Contains(primary) || primary == "ru")
            {
                return Ru;
            }

            return primary switch
            {
                "es" => Es,
                "fa" => Fa,
                "ar" => Ar,
                _ => En,
            };
        }

        /// <summary>
        /// Maps Accept-Language / Telegram language codes to a supported bot language: ru, es, fa, ar, or en.
        /// </summary>
        public static string NormalizeLanguage(string? langOrAcceptLanguage)
        {
            return GetCulture(langOrAcceptLanguage).TwoLetterISOLanguageName;
        }

        public static string CurrentLanguage() =>
            NormalizeLanguage(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

        private static string? GetPrimaryLanguageTag(string? langCode)
        {
            if (string.IsNullOrWhiteSpace(langCode))
            {
                return null;
            }

            var primary = langCode.Split(',')[0].Trim();
            var semicolon = primary.IndexOf(';');
            if (semicolon >= 0)
            {
                primary = primary[..semicolon].Trim();
            }

            var dash = primary.IndexOf('-');
            if (dash >= 0)
            {
                primary = primary[..dash];
            }

            return string.IsNullOrWhiteSpace(primary) ? null : primary.ToLowerInvariant();
        }
    }
}
