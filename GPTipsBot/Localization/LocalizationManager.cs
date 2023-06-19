using System.Globalization;

namespace GPTipsBot.Localization
{
    public class LocalizationManager
    {
        public static CultureInfo[] SupportedCultures;
        public static readonly CultureInfo Ru;
        public static readonly CultureInfo En;

        // Страны СНГ
        public static string[] CisCountries;

        static LocalizationManager()
        {
            Ru = new CultureInfo("ru");
            En = new CultureInfo("en");
            SupportedCultures = new CultureInfo[] { En, Ru };
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
            if (CisCountries.Contains(langCode))
            {
                return Ru;
            }

            return En;
        }
    }
}
