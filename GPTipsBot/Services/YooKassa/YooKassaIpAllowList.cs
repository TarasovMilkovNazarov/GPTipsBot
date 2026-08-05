using System.Net;
using System.Net.Sockets;

namespace GPTipsBot.Services.YooKassa;

/// <summary>
/// Official YooKassa notification source IP ranges:
/// https://yookassa.ru/developers/using-api/webhooks
/// </summary>
public static class YooKassaIpAllowList
{
    private static readonly (IPAddress Network, int Prefix)[] V4Ranges =
    [
        (IPAddress.Parse("185.71.76.0"), 27),
        (IPAddress.Parse("185.71.77.0"), 27),
        (IPAddress.Parse("77.75.153.0"), 25),
        (IPAddress.Parse("77.75.154.128"), 25)
    ];

    private static readonly HashSet<IPAddress> V4Singles = new()
    {
        IPAddress.Parse("77.75.156.11"),
        IPAddress.Parse("77.75.156.35")
    };

    private static readonly (IPAddress Network, int Prefix) V6Range =
        (IPAddress.Parse("2a02:5180::"), 32);

    public static bool IsAllowed(IPAddress? ip)
    {
        if (ip == null)
        {
            return false;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            if (V4Singles.Contains(ip))
            {
                return true;
            }

            return V4Ranges.Any(range => IsInCidr(ip, range.Network, range.Prefix));
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return IsInCidr(ip, V6Range.Network, V6Range.Prefix);
        }

        return false;
    }

    /// <summary>True if any hop in X-Forwarded-For is a YooKassa notification IP.</summary>
    public static bool IsAllowedFromForwarded(string? xForwardedFor)
    {
        if (string.IsNullOrWhiteSpace(xForwardedFor))
        {
            return false;
        }

        foreach (var part in xForwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPAddress.TryParse(part, out var ip) && IsAllowed(ip))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInCidr(IPAddress address, IPAddress network, int prefixLength)
    {
        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        if (addressBytes.Length != networkBytes.Length)
        {
            return false;
        }

        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (var i = 0; i < fullBytes; i++)
        {
            if (addressBytes[i] != networkBytes[i])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (addressBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
    }
}
