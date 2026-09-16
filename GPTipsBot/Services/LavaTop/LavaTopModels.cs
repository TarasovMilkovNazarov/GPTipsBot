using Newtonsoft.Json;

namespace GPTipsBot.Services.LavaTop;

// Field names below are copied verbatim from the live spec (gate.lava.top/docs/documentation.yaml) —
// lava.top mixes camelCase (email, offerId, amount) with snake_case (successful_return_url) in the same
// request, so each field carries an explicit [JsonProperty] rather than a blanket naming strategy.

public class CreateLavaTopInvoiceRequest
{
    [JsonProperty("email")]
    public string Email { get; set; } = null!;

    [JsonProperty("offerId")]
    public string OfferId { get; set; } = null!;

    [JsonProperty("currency")]
    public string Currency { get; set; } = null!;

    /// <summary>Overrides the offer's price. Only accepted for a "price on request" offer.</summary>
    [JsonProperty("amount", NullValueHandling = NullValueHandling.Ignore)]
    public decimal? Amount { get; set; }

    [JsonProperty("successful_return_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? SuccessfulReturnUrl { get; set; }

    [JsonProperty("failure_return_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? FailureReturnUrl { get; set; }

    [JsonProperty("cancel_return_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? CancelReturnUrl { get; set; }
}

/// <summary>Response to POST /api/v3/invoice (HTTP 201).</summary>
public class LavaTopInvoiceResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    /// <summary>new / in-progress / completed / failed / cancelled / subscription-* (lowercase-hyphenated).</summary>
    [JsonProperty("status")]
    public string Status { get; set; } = null!;

    /// <summary>Null when the product is free — never the case for a gem top-up.</summary>
    [JsonProperty("paymentUrl")]
    public string? PaymentUrl { get; set; }
}

/// <summary>Response to GET /api/v1/invoices/{id} — the authoritative status + amount for confirmation.</summary>
public class LavaTopInvoiceDetails
{
    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    /// <summary>NEW / IN_PROGRESS / COMPLETED / FAILED (upper snake — a different enum spelling than the
    /// create-response status, confirmed against the spec).</summary>
    [JsonProperty("status")]
    public string Status { get; set; } = null!;

    [JsonProperty("receipt")]
    public LavaTopReceipt? Receipt { get; set; }

    [JsonProperty("buyer")]
    public LavaTopBuyer? Buyer { get; set; }
}

public class LavaTopReceipt
{
    /// <summary>0 for a free product — major units (e.g. 5.99), not cents.</summary>
    [JsonProperty("amount")]
    public decimal Amount { get; set; }

    [JsonProperty("currency")]
    public string Currency { get; set; } = null!;

    [JsonProperty("fee")]
    public decimal Fee { get; set; }
}

public class LavaTopBuyer
{
    [JsonProperty("email")]
    public string? Email { get; set; }
}

/// <summary>Payload lava.top POSTs to the configured webhook URL.</summary>
public class LavaTopWebhookPayload
{
    /// <summary>payment.success / payment.failed / subscription.* — only payment.success is credited.</summary>
    [JsonProperty("eventType")]
    public string EventType { get; set; } = null!;

    [JsonProperty("contractId")]
    public string? ContractId { get; set; }

    [JsonProperty("buyer")]
    public LavaTopBuyer? Buyer { get; set; }

    [JsonProperty("amount")]
    public decimal Amount { get; set; }

    [JsonProperty("currency")]
    public string? Currency { get; set; }

    /// <summary>Mirrors LavaTopInvoiceResponse.Status (lowercase-hyphenated), e.g. "completed".</summary>
    [JsonProperty("status")]
    public string? Status { get; set; }

    [JsonProperty("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public class LavaTopErrorResponse
{
    [JsonProperty("error")]
    public string? Error { get; set; }
}
