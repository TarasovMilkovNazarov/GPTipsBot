using System.Globalization;

namespace GPTipsBot.Localization;

/// <summary>
/// Sets CurrentUICulture for this async flow only (AsyncLocal in .NET) and restores it after.
/// Parallel Telegram updates each get their own flow, so one user's language cannot overwrite another's.
/// </summary>
public readonly struct UiCultureScope : IDisposable
{
    private readonly CultureInfo _previous;

    public UiCultureScope(CultureInfo culture)
    {
        _previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static UiCultureScope ForLanguage(string? langCode) =>
        new(LocalizationManager.GetCulture(string.IsNullOrWhiteSpace(langCode) ? "ru" : langCode));

    public void Dispose() => CultureInfo.CurrentUICulture = _previous;
}
