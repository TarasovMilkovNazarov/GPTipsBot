namespace GPTipsBot.Models;

/// <summary>
/// Tracks that a browser fingerprint already received the one-time guest free quota.
/// VisitorId comes from FingerprintJS (OSS) or Fingerprint Pro — same field either way.
/// </summary>
public class GuestFingerprintQuota
{
    /// <summary>FingerprintJS / Pro visitor identifier.</summary>
    public string Fingerprint { get; set; } = "";

    public long GuestUserId { get; set; }

    /// <summary>SHA-256 hex of client IP (privacy-preserving soft rate limit).</summary>
    public string? IpHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
