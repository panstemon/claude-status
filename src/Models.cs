namespace ClaudeStatus;

/// <summary>A single rate-limit window returned by the usage endpoint.</summary>
public sealed record UsageWindow(double Utilization, DateTimeOffset? ResetsAt);

/// <summary>
/// A point-in-time snapshot of all the limit windows we care about.
/// Any window may be null when the plan/account does not expose it.
/// </summary>
public sealed class UsageSnapshot
{
    public UsageWindow? FiveHour { get; init; }
    public UsageWindow? SevenDay { get; init; }
    public UsageWindow? SevenDayOpus { get; init; }
    public UsageWindow? SevenDaySonnet { get; init; }
    public bool ExtraUsageEnabled { get; init; }
    public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.Now;

    /// <summary>The most-constrained utilization across all known windows (drives the tray badge).</summary>
    public double MaxUtilization
    {
        get
        {
            double max = 0;
            foreach (var w in new[] { FiveHour, SevenDay, SevenDayOpus, SevenDaySonnet })
                if (w is not null && w.Utilization > max) max = w.Utilization;
            return max;
        }
    }
}

/// <summary>The Claude.ai OAuth credentials stored by Claude Code in ~/.claude/.credentials.json.</summary>
public sealed class OAuthCredentials
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    /// <summary>Expiry as Unix epoch milliseconds (matches the on-disk format).</summary>
    public long ExpiresAt { get; set; }

    public bool IsExpiredOrExpiringSoon =>
        ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 60_000;
}
