using GPTipsBot.Models;

namespace GPTipsBot.Config;

/// <summary>
/// lava.top (gate.lava.top) — a merchant-of-record rail for international card / PayPal payments,
/// settled in RUB, USD or EUR (never gems or XTR). Unlike YooKassa, it needs one pre-created "offer" in
/// the lava.top dashboard with "Price by request through API" enabled; every top-up is an invoice
/// against that single offer with a per-request <c>amount</c> override, so no per-package product setup
/// is needed on their side.
/// </summary>
/// <remarks>
/// Schema verified against the live spec at https://gate.lava.top/docs/documentation.yaml (2026-09-16):
/// POST /api/v3/invoice (request: email, offerId, currency, optional amount/paymentProvider/return
/// urls; response: id, status, paymentUrl) and GET /api/v1/invoices/{id} (authoritative status +
/// receipt.amount/currency — lava.top's own docs warn the redirect query string is forgeable and only
/// the webhook + this GET are a source of truth).
/// </remarks>
public static class LavaTopConfig
{
    public static bool IsEnabled =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(OfferId) && GemsPerUnit > 0;

    /// <summary>Outbound key sent as X-Api-Key to gate.lava.top (Integrations → Public API → API keys).</summary>
    public static string? ApiKey => Normalize(Environment.GetEnvironmentVariable("LAVATOP_API_KEY"));

    /// <summary>
    /// UUID of the dynamic-price offer created in the lava.top dashboard. Every package is this same
    /// offer with a different <c>amount</c> — lava.top rejects <c>amount</c> on a fixed-price offer.
    /// </summary>
    public static string? OfferId => Normalize(Environment.GetEnvironmentVariable("LAVATOP_OFFER_ID"));

    /// <summary>
    /// Inbound secret lava.top echoes back in the X-Api-Key header on every webhook call (Integrations →
    /// Public API → Add Webhook → "API key" auth — pick any value and paste it both there and here).
    /// Without this a webhook cannot be trusted at all, so the endpoint hard-rejects when it's unset.
    /// </summary>
    public static string? WebhookApiKey => Normalize(Environment.GetEnvironmentVariable("LAVATOP_WEBHOOK_API_KEY"));

    /// <summary>RUB, USD or EUR — the only currencies lava.top settles in.</summary>
    public static string Currency =>
        Normalize(Environment.GetEnvironmentVariable("LAVATOP_CURRENCY"))?.ToUpperInvariant()
        ?? CurrencyCode.Usd;

    /// <summary>
    /// Gems credited per 1 unit of <see cref="Currency"/>. Deliberately has no fallback: a stale
    /// RUB-per-gem-shaped default would be off by the RUB/USD exchange rate and silently mischarge.
    /// 0 means "not priced yet", which <see cref="IsEnabled"/> callers must treat as disabled.
    /// </summary>
    public static int GemsPerUnit
    {
        get
        {
            var raw = Normalize(Environment.GetEnvironmentVariable("LAVATOP_GEMS_PER_UNIT"));
            return int.TryParse(raw, out var value) && value > 0 ? value : 0;
        }
    }

    /// <summary>
    /// Smallest top-up lava.top is offered for, in <see cref="Currency"/> units. Independent of
    /// <see cref="PaymentConfig.MinRechargeGems"/> — that floor is YooKassa/Stars' own RUB minimum
    /// expressed in gems and must never gate this rail.
    /// </summary>
    /// <remarks>
    /// $5 is not our own choice — lava.top's own API rejects a USD invoice below it (confirmed live:
    /// <c>POST /api/v3/invoice</c> returns <c>"Amount=1.25 not in allowed limits=(5, 10000) for USD"</c>).
    /// Setting this below lava.top's real per-currency floor just moves the failure from a clean
    /// client-side message to a raw API error, so re-check the actual limits before lowering it,
    /// especially if <see cref="Currency"/> is changed to RUB or EUR — their floors may differ.
    /// </remarks>
    public static decimal MinRechargeAmount
    {
        get
        {
            var raw = Normalize(Environment.GetEnvironmentVariable("LAVATOP_MIN_AMOUNT"));
            return decimal.TryParse(raw, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0
                ? value
                : 5m;
        }
    }

    /// <summary>
    /// Largest top-up lava.top is offered for, in <see cref="Currency"/> units — same reasoning as
    /// <see cref="MinRechargeAmount"/>: lava.top's own USD ceiling is 10000, confirmed by the same error.
    /// </summary>
    public static decimal MaxRechargeAmount
    {
        get
        {
            var raw = Normalize(Environment.GetEnvironmentVariable("LAVATOP_MAX_AMOUNT"));
            return decimal.TryParse(raw, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0
                ? value
                : 10_000m;
        }
    }

    /// <summary>
    /// Gem packages shown on /deposit for this rail — 5, 10 and 25 units of <see cref="Currency"/>,
    /// anchored just above lava.top's own $5 floor. No usage data exists yet for this rail (unlike the
    /// RUB/Stars ladders, which were trimmed to their real payment history), so this mirrors that same
    /// "start small" pattern rather than assuming international payers spend more; a bigger amount is
    /// still one manual entry away. Empty while unpriced, same as every other lava.top-gated list here.
    /// </summary>
    public static int[] DepositPackages =>
        GemsPerUnit > 0
            ? [.. new[] { 5, 10, 25 }.Select(units => units * GemsPerUnit)]
            : [];

    /// <summary>URL where the user returns after paying. Falls back to the YooKassa one — same bot/cabinet.</summary>
    public static string ReturnUrl =>
        Normalize(Environment.GetEnvironmentVariable("LAVATOP_RETURN_URL")) ?? YooKassaConfig.ReturnUrl;

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('"', '\'');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
