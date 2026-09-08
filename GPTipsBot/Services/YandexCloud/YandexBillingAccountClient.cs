using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using GPTipsBot.Config;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Services.YandexCloud;

public record YandexBillingAccount(string Id, string Name, decimal Balance, bool Active);

public class YandexBillingAccountClient(HttpClient httpClient, ILogger<YandexBillingAccountClient> logger)
{
    private const string MetadataTokenUrl =
        "http://169.254.169.254/computeMetadata/v1/instance/service-accounts/default/token";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<YandexBillingAccount>> ListAccountsAsync(CancellationToken cancellationToken)
    {
        var token = await GetIamTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://billing.api.cloud.yandex.net/billing/v1/billingAccounts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Yandex Billing list accounts failed with {(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);
        }

        var parsed = JsonSerializer.Deserialize<ListBillingAccountsResponse>(body, JsonOptions)
                     ?? new ListBillingAccountsResponse();
        return parsed.BillingAccounts
            .Select(a => new YandexBillingAccount(
                a.Id,
                string.IsNullOrWhiteSpace(a.Name) ? a.Id : a.Name,
                decimal.TryParse(a.Balance, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var balance)
                    ? balance
                    : 0m,
                a.Active))
            .ToArray();
    }

    private async Task<string> GetIamTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(AppConfig.YandexIamToken))
            return AppConfig.YandexIamToken;

        using var request = new HttpRequestMessage(HttpMethod.Get, MetadataTokenUrl);
        request.Headers.TryAddWithoutValidation("Metadata-Flavor", "Google");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));

        using var response = await httpClient.SendAsync(request, timeout.Token);
        var body = await response.Content.ReadAsStringAsync(timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Yandex metadata IAM token failed with {(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);
        }

        var parsed = JsonSerializer.Deserialize<MetadataTokenResponse>(body, JsonOptions);
        if (string.IsNullOrWhiteSpace(parsed?.AccessToken))
            throw new InvalidOperationException(
                $"Yandex metadata did not return access_token (response length {body.Length})");

        logger.LogDebug("Got IAM token from VM metadata, expires_in={ExpiresIn}", parsed.ExpiresIn);
        return parsed.AccessToken;
    }

    private sealed class ListBillingAccountsResponse
    {
        public List<BillingAccountDto> BillingAccounts { get; set; } = [];
    }

    private sealed class BillingAccountDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Balance { get; set; } = "0";
        public bool Active { get; set; }
    }

    private sealed class MetadataTokenResponse
    {
        // Метадата отдаёт snake_case, а PropertyNameCaseInsensitive подчёркивания не игнорирует.
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
