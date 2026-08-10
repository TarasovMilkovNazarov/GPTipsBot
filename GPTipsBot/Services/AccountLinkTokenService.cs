using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GPTipsBot.Config;

namespace GPTipsBot.Services;

/// <summary>
/// Short-lived HMAC tokens for Telegram deep-link account linking (?start=l_...).
/// Fits Telegram's 64-character start parameter limit.
/// </summary>
public class AccountLinkTokenService
{
    public const string Prefix = "l_";
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    private const int UserIdBytes = 8;
    private const int ExpBytes = 4;
    private const int MacBytes = 8;
    private const int PayloadBytes = UserIdBytes + ExpBytes + MacBytes;

    public string Create(long userId, TimeSpan? ttl = null)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        var lifetime = ttl ?? DefaultTtl;
        var exp = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        if (exp > uint.MaxValue)
        {
            exp = uint.MaxValue;
        }

        Span<byte> payload = stackalloc byte[PayloadBytes];
        BinaryPrimitives.WriteInt64BigEndian(payload[..UserIdBytes], userId);
        BinaryPrimitives.WriteUInt32BigEndian(payload.Slice(UserIdBytes, ExpBytes), (uint)exp);
        ComputeMac(payload[..(UserIdBytes + ExpBytes)], payload.Slice(UserIdBytes + ExpBytes, MacBytes));

        return Prefix + Base64UrlEncode(payload);
    }

    public bool TryValidate(string? token, out long userId)
    {
        userId = 0;
        if (string.IsNullOrWhiteSpace(token) ||
            !token.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var encoded = token[Prefix.Length..];
        Span<byte> payload = stackalloc byte[PayloadBytes];
        if (!TryBase64UrlDecode(encoded, payload, out var written) || written != PayloadBytes)
        {
            return false;
        }

        Span<byte> expectedMac = stackalloc byte[MacBytes];
        ComputeMac(payload[..(UserIdBytes + ExpBytes)], expectedMac);
        if (!CryptographicOperations.FixedTimeEquals(
                payload.Slice(UserIdBytes + ExpBytes, MacBytes),
                expectedMac))
        {
            return false;
        }

        var exp = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(UserIdBytes, ExpBytes));
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
        {
            return false;
        }

        userId = BinaryPrimitives.ReadInt64BigEndian(payload[..UserIdBytes]);
        return userId > 0;
    }

    public bool IsLinkToken(string? token) =>
        !string.IsNullOrWhiteSpace(token) &&
        token.StartsWith(Prefix, StringComparison.Ordinal);

    private static void ComputeMac(ReadOnlySpan<byte> data, Span<byte> destination)
    {
        var key = Encoding.UTF8.GetBytes(AppConfig.TelegramToken);
        Span<byte> full = stackalloc byte[32];
        HMACSHA256.HashData(key, data, full);
        full[..MacBytes].CopyTo(destination);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        var base64 = Convert.ToBase64String(data);
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool TryBase64UrlDecode(string encoded, Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;
        var padded = encoded.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
            case 1: return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(padded);
            if (bytes.Length > destination.Length)
            {
                return false;
            }

            bytes.CopyTo(destination);
            bytesWritten = bytes.Length;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
