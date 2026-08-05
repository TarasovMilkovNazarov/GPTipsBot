using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GPTipsBot.Services.YooKassa;

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class YooKassaAmount
{
    public string Value { get; set; } = null!;
    public string Currency { get; set; } = "RUB";
}

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class YooKassaConfirmationRequest
{
    public string Type { get; set; } = "redirect";
    public string ReturnUrl { get; set; } = null!;
}

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class YooKassaConfirmationResponse
{
    public string Type { get; set; } = null!;
    public string? ConfirmationUrl { get; set; }
}

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class CreateYooKassaPaymentRequest
{
    public YooKassaAmount Amount { get; set; } = null!;
    public bool Capture { get; set; } = true;
    public YooKassaConfirmationRequest Confirmation { get; set; } = null!;
    public string? Description { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class YooKassaPayment
{
    public string Id { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool Paid { get; set; }
    public YooKassaAmount Amount { get; set; } = null!;
    public YooKassaConfirmationResponse? Confirmation { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public bool Test { get; set; }
}

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class YooKassaNotification
{
    public string Type { get; set; } = null!;
    public string Event { get; set; } = null!;
    public YooKassaPayment Object { get; set; } = null!;
}
