using System.Net.Http.Headers;
using System.Text;
using GPTipsBot.Config;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GPTipsBot.Services.LavaTop;

public class LavaTopClient
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<LavaTopClient> _logger;

    public LavaTopClient(HttpClient httpClient, ILogger<LavaTopClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress ??= new Uri("https://gate.lava.top/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<LavaTopInvoiceResponse> CreateInvoiceAsync(
        CreateLavaTopInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v3/invoice");
        ApplyAuth(message);
        message.Content = new StringContent(
            JsonConvert.SerializeObject(request, JsonSettings),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "lava.top create invoice failed: {Status} {Body} offerId={OfferId} apiKey={MaskedKey}",
                response.StatusCode, body, request.OfferId, MaskSecret(LavaTopConfig.ApiKey));
            var error = TryParseError(body);
            throw new InvalidOperationException(
                $"lava.top create invoice failed: {response.StatusCode} {error ?? body}");
        }

        _logger.LogInformation("lava.top create invoice OK: {BodyPreview}",
            body.Length <= 300 ? body : body[..300] + "...");

        var invoice = JsonConvert.DeserializeObject<LavaTopInvoiceResponse>(body, JsonSettings);
        if (invoice == null || string.IsNullOrWhiteSpace(invoice.Id))
        {
            throw new InvalidOperationException("lava.top returned an empty invoice");
        }

        return invoice;
    }

    /// <summary>
    /// Authoritative status/amount lookup — lava.top's own docs warn the return-URL query string is
    /// forgeable and only the webhook + this call are a source of truth for the payment state.
    /// </summary>
    public async Task<LavaTopInvoiceDetails> GetInvoiceAsync(string invoiceId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"api/v1/invoices/{invoiceId}");
        ApplyAuth(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "lava.top get invoice failed: {Status} {Body} invoiceId={InvoiceId} apiKey={MaskedKey}",
                response.StatusCode, body, invoiceId, MaskSecret(LavaTopConfig.ApiKey));
            var error = TryParseError(body);
            throw new InvalidOperationException(
                $"lava.top get invoice failed: {response.StatusCode} {error ?? body}");
        }

        _logger.LogInformation("lava.top get invoice OK id={InvoiceId}: {BodyPreview}",
            invoiceId,
            body.Length <= 300 ? body : body[..300] + "...");

        var invoice = JsonConvert.DeserializeObject<LavaTopInvoiceDetails>(body, JsonSettings);
        if (invoice == null)
        {
            throw new InvalidOperationException("lava.top returned an empty invoice");
        }

        return invoice;
    }

    /// <summary>
    /// First/last 4 chars with the middle blanked out — enough to confirm in logs which configured key
    /// actually went out on the wire (e.g. distinguishing it from LAVATOP_WEBHOOK_API_KEY, a common
    /// mix-up) without ever writing the real secret anywhere.
    /// </summary>
    private static string MaskSecret(string? value, int visibleChars = 4)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "(unset)";
        }

        if (value.Length <= visibleChars * 2)
        {
            return new string('*', value.Length);
        }

        return $"{value[..visibleChars]}...........{value[^visibleChars..]}";
    }

    private static string? TryParseError(string body)
    {
        try
        {
            return JsonConvert.DeserializeObject<LavaTopErrorResponse>(body)?.Error;
        }
        catch
        {
            return null;
        }
    }

    private static void ApplyAuth(HttpRequestMessage message)
    {
        if (!LavaTopConfig.IsEnabled)
        {
            throw new InvalidOperationException("lava.top is not configured");
        }

        message.Headers.Add("X-Api-Key", LavaTopConfig.ApiKey);
    }
}
