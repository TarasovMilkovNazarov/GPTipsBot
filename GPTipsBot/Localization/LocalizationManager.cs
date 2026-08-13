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

        /// <summary>
        /// Telegram language codes and ISO country codes that should get the Russian UI / ru broadcast text.
        /// Telegram sends uk/be/kk; older rows and some clients store ua/by/kz instead.
        /// </summary>
        private static readonly HashSet<string> RussianAudienceTags;

        static LocalizationManager()
        {
            Ru = new CultureInfo("ru");
            En = new CultureInfo("en");
            Es = new CultureInfo("es");
            Fa = new CultureInfo("fa");
            Ar = new CultureInfo("ar");
            SupportedCultures = new CultureInfo[] { En, Ru, Es, Fa, Ar };
            RussianAudienceTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ru",
                "uk", "be", "kk", "ky", "uz", "tg", "tk", "hy", "az", "ka", "mo",
                "ua", "by", "kz", "kg", "tj", "tm", "am", "ge", "md",
            };
        }

        public static CultureInfo GetCulture(string? langCode)
        {
            var primary = GetPrimaryLanguageTag(langCode);
            if (primary == null)
            {
                return En;
            }

            if (RussianAudienceTags.Contains(primary))
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

        /// <summary>
        /// Raw BotSettings.Language values that NormalizeLanguage maps onto <paramref name="normalized"/>.
        /// Used for SQL IN filters where the DB still has uk, ru-RU, EN, etc.
        /// </summary>
        public static string[] DatabaseTagsFor(string normalized)
        {
            var target = NormalizeLanguage(normalized);
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { target };
            if (target == "ru")
            {
                foreach (var tag in RussianAudienceTags)
                {
                    tags.Add(tag);
                }

                tags.Add("ru-ru");
                tags.Add("ru-RU");
            }
            else if (target == "en")
            {
                tags.Add("en-us");
                tags.Add("en-gb");
                tags.Add("en-US");
                tags.Add("en-GB");
            }

            return tags.Select(t => t.ToLowerInvariant()).Distinct().ToArray();
        }

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

            var separator = primary.IndexOfAny(['-', '_']);
            if (separator >= 0)
            {
                primary = primary[..separator];
            }

            return string.IsNullOrWhiteSpace(primary) ? null : primary.ToLowerInvariant();
        }
    }
}
