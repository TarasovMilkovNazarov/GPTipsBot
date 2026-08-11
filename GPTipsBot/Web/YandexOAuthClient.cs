using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using GPTipsBot.Config;

namespace GPTipsBot.Web;

public sealed class YandexOAuthClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<YandexTokenResponse> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        if (!YandexOAuthConfig.IsEnabled)
        {
            throw new InvalidOperationException("Yandex OAuth is not configured");
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = YandexOAuthConfig.ClientId!,
            ["client_secret"] = YandexOAuthConfig.ClientSecret!,
            ["redirect_uri"] = redirectUri,
        });

        using var response = await http.PostAsync(YandexOAuthConfig.TokenUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Yandex token exchange failed: {(int)response.StatusCode} {body}");
        }

        var token = JsonSerializer.Deserialize<YandexTokenResponse>(body, JsonOptions)
                    ?? throw new InvalidOperationException("Empty Yandex token response");
        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("Yandex token response missing access_token");
        }

        return token;
    }

    public async Task<YandexUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{YandexOAuthConfig.UserInfoUrl}?format=json");
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Yandex user info failed: {(int)response.StatusCode} {body}");
        }

        var info = JsonSerializer.Deserialize<YandexUserInfo>(body, JsonOptions)
                   ?? throw new InvalidOperationException("Empty Yandex user info");
        if (string.IsNullOrWhiteSpace(info.Id))
        {
            throw new InvalidOperationException("Yandex user info missing id");
        }

        return info;
    }
}

public sealed class YandexTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; }
}

public sealed class YandexUserInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("login")]
    public string? Login { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("default_email")]
    public string? DefaultEmail { get; set; }

    [JsonPropertyName("real_name")]
    public string? RealName { get; set; }
}
