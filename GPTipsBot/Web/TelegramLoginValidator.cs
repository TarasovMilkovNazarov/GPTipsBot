using System.Security.Cryptography;
using System.Text;
using GPTipsBot.Config;

namespace GPTipsBot.Web;

public sealed record TelegramLoginPayload(
    long Id,
    string FirstName,
    string? LastName,
    string? Username,
    string? PhotoUrl,
    long AuthDate,
    string Hash);

public static class TelegramLoginValidator
{
    /// <summary>
    /// Validates Telegram Login Widget data per https://core.telegram.org/widgets/login#checking-authorization
    /// </summary>
    public static bool TryValidate(TelegramLoginPayload payload, out string? error, TimeSpan? maxAge = null)
    {
        error = null;
        maxAge ??= TimeSpan.FromDays(1);

        if (payload.Id <= 0 || string.IsNullOrWhiteSpace(payload.Hash))
        {
            error = "Invalid Telegram login payload";
            return false;
        }

        var authUtc = DateTimeOffset.FromUnixTimeSeconds(payload.AuthDate);
        if (DateTimeOffset.UtcNow - authUtc > maxAge)
        {
            error = "Telegram login expired";
            return false;
        }

        var dataCheckString = BuildDataCheckString(payload);
        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(AppConfig.TelegramToken));
        var hashBytes = HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
        var computed = Convert.ToHexString(hashBytes).ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(payload.Hash.ToLowerInvariant())))
        {
            error = "Invalid Telegram login signature";
            return false;
        }

        return true;
    }

    private static string BuildDataCheckString(TelegramLoginPayload payload)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = payload.AuthDate.ToString(),
            ["first_name"] = payload.FirstName,
            ["id"] = payload.Id.ToString(),
        };

        if (!string.IsNullOrEmpty(payload.LastName))
            fields["last_name"] = payload.LastName;
        if (!string.IsNullOrEmpty(payload.Username))
            fields["username"] = payload.Username;
        if (!string.IsNullOrEmpty(payload.PhotoUrl))
            fields["photo_url"] = payload.PhotoUrl;

        return string.Join('\n', fields.Select(kv => $"{kv.Key}={kv.Value}"));
    }
}
