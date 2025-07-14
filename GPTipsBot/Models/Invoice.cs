namespace GPTipsBot.Models;

public class Invoice: Entity
{
    public long UserId { get; set; }
    public long Amount { get; set; }
    public string Currency { get; set; }
    public InvoiceStatus Status { get; set; }
    public long TelegramInvoiceId { get; set; }
}

public enum InvoiceStatus
{
    Created,
    Paid,
    Expired,
    Failed,
    Refunded
}

public class Currency
{
    public const string Stars = "XTR";
}