using System.Net.Http.Headers;
using System.Text;
using GPTipsBot.Config;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GPTipsBot.Services.YooKassa;

public class YooKassaClient
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        },
        NullValueHandling = NullValueHandling.Ignore
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<YooKassaClient> _logger;

    public YooKassaClient(HttpClient httpClient, ILogger<YooKassaClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress ??= new Uri("https://api.yookassa.ru/v3/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<YooKassaPayment> CreatePaymentAsync(
        CreateYooKassaPaymentRequest request,
        string idempotenceKey,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "payments");
        ApplyAuth(message);
        message.Headers.Add("Idempotence-Key", idempotenceKey);
        message.Content = new StringContent(
            JsonConvert.SerializeObject(request, JsonSettings),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("YooKassa create payment failed: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"YooKassa create payment failed: {(int)response.StatusCode}");
        }

        var payment = JsonConvert.DeserializeObject<YooKassaPayment>(body, JsonSettings);
        if (payment == null || string.IsNullOrWhiteSpace(payment.Id))
        {
            throw new InvalidOperationException("YooKassa returned an empty payment");
        }

        return payment;
    }

    public async Task<YooKassaPayment> GetPaymentAsync(string paymentId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"payments/{paymentId}");
        ApplyAuth(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("YooKassa get payment failed: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"YooKassa get payment failed: {(int)response.StatusCode}");
        }

        var payment = JsonConvert.DeserializeObject<YooKassaPayment>(body, JsonSettings);
        if (payment == null)
        {
            throw new InvalidOperationException("YooKassa returned an empty payment");
        }

        return payment;
    }

    private static void ApplyAuth(HttpRequestMessage message)
    {
        if (!YooKassaConfig.IsEnabled)
        {
            throw new InvalidOperationException("YooKassa is not configured");
        }

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{YooKassaConfig.ShopId}:{YooKassaConfig.SecretKey}"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }
}
