using System.Globalization;

namespace GPTipsBot.Services.YandexCloud;

public static class YandexBillingBalanceAlerts
{
    /// <summary>
    /// Уведомлять, когда баланс лицевого счёта становится строго меньше порога.
    /// 0 / -1000 / -2000 — долг, не остаток на счёте.
    /// </summary>
    public static readonly decimal[] Thresholds = [0m, -1000m, -2000m];

    public static IReadOnlyList<decimal> NewlyCrossed(decimal balance, IReadOnlySet<decimal> alreadyNotified)
    {
        return Thresholds.Where(t => balance < t && !alreadyNotified.Contains(t)).ToArray();
    }

    public static IReadOnlyList<decimal> Recovered(decimal balance, IReadOnlySet<decimal> alreadyNotified)
    {
        return alreadyNotified.Where(t => balance >= t).ToArray();
    }

    public static string FormatMessage(string accountName, decimal balance, IReadOnlyList<decimal> crossed)
    {
        var balanceText = balance.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"));
        var thresholdsText = string.Join(", ", crossed.Select(FormatThreshold));
        var header = crossed.Count == 1 ? "Сработал порог" : "Сработали пороги";
        return $"Yandex Cloud: баланс «{accountName}» {balanceText} ₽\n{header}: {thresholdsText}";
    }

    public static string FormatThreshold(decimal threshold) =>
        $"ниже {threshold.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"))} ₽";
}
