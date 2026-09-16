namespace GPTipsBot.Models;

public class Invoice: Entity
{
    public long UserId { get; set; }
    /// <summary>Amount to credit in gems (wallet units), whatever the rail charged.</summary>
    public long Amount { get; set; }
    /// <summary>Currency the payer was charged in — XTR for Telegram Stars, RUB for YooKassa.</summary>
    public string Currency { get; set; } = CurrencyCode.Stars;
    public InvoiceStatus Status { get; set; }
    public long TelegramInvoiceId { get; set; }
    public PaymentProvider Provider { get; set; } = PaymentProvider.TelegramStars;
    /// <summary>YooKassa payment id / lava.top contract id (or other external provider id).</summary>
    public string? ExternalPaymentId { get; set; }
    /// <summary>
    /// Fiat amount in minor units (kopecks for RUB, cents for USD/EUR) of whatever currency the invoice
    /// was actually raised in — see <see cref="Currency"/>, not assumed to be RUB.
    /// </summary>
    public long? FiatAmountKopecks { get; set; }
}

public enum InvoiceStatus
{
    Created,
    Paid,
    Expired,
    Failed,
    Refunded
}

public enum PaymentProvider
{
    TelegramStars = 0,
    YooKassa = 1,
    LavaTop = 2
}

public static class CurrencyCode
{
    /// <summary>Telegram Stars, the payment rail — never the wallet unit.</summary>
    public const string Stars = "XTR";
    public const string Rub = "RUB";
    public const string Usd = "USD";
    public const string Eur = "EUR";
    /// <summary>The wallet unit (gems, 💎).</summary>
    public const string Gem = "GEM";
}

/// <summary>Backward-compatible alias used across the codebase.</summary>
public static class Currency
{
    public const string Stars = CurrencyCode.Stars;
}
