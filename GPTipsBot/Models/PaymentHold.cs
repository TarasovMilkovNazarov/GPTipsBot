namespace GPTipsBot.Models;

public enum PaidFeature
{
    Gpt = 0,
    Image = 1,
    TextRecognition = 2,
    Animation = 3,
    Summary = 4,
    GptImage = 5,
    WatermarkRemoval = 6,
}

public enum PaymentHoldStatus
{
    Held = 0,
    Confirmed = 1,
    Released = 2,
}

public class PaymentHold : Entity
{
    public long UserId { get; set; }
    public PaidFeature Feature { get; set; }
    public bool UsedFreeQuota { get; set; }
    public double WalletAmount { get; set; }
    public PaymentHoldStatus Status { get; set; } = PaymentHoldStatus.Held;
}
