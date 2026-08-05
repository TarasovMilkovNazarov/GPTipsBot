namespace GPTipsBot.Models;

public class Invoice: Entity
{
    public long UserId { get; set; }
    /// <summary>Amount to credit in Stars (wallet units).</summary>
    public long Amount { get; set; }
    public string Currency { get; set; } = CurrencyCode.Stars;
    public InvoiceStatus Status { get; set; }
    public long TelegramInvoiceId { get; set; }
    public PaymentProvider Provider { get; set; } = PaymentProvider.TelegramStars;
    /// <summary>YooKassa payment id (or other external provider id).</summary>
    public string? ExternalPaymentId { get; set; }
    /// <summary>Fiat amount in kopecks for YooKassa payments.</summary>
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
    YooKassa = 1
}

public static class CurrencyCode
{
    public const string Stars = "XTR";
    public const string Rub = "RUB";
}

/// <summary>Backward-compatible alias used across the codebase.</summary>
public static class Currency
{
    public const string Stars = CurrencyCode.Stars;
}
