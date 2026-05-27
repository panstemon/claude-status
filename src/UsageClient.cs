using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ClaudeStatus;

/// <summary>
/// Fetches live subscription usage from Anthropic's (undocumented) OAuth usage
/// endpoint, transparently refreshing the access token when it is expired.
/// </summary>
public sealed class UsageClient : IDisposable
{
    private const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";
    private const string TokenUrl = "https://claude.ai/v1/oauth/token";
    private const string OAuthClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
    private const string OAuthBeta = "oauth-2025-04-20";

    // The User-Agent must look like claude-code or the request lands in an
    // aggressively rate-limited bucket.
    private const string UserAgent = "claude-code/2.1.152";

    private readonly CredentialStore _store;
    private readonly HttpClient _http;

    public UsageClient(CredentialStore? store = null)
    {
        _store = store ?? new CredentialStore();
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    /// <summary>Returns the current usage snapshot, refreshing the token if needed.</summary>
    public async Task<UsageSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        if (!_store.Exists)
            throw new InvalidOperationException(
                "Claude credentials not found. Sign in with Claude Code first.");

        var creds = _store.Read();
        if (creds.IsExpiredOrExpiringSoon)
            creds = await RefreshAsync(creds, ct);

        var response = await SendUsageRequestAsync(creds.AccessToken, ct);

        // Token may have been revoked/rotated out from under us; refresh once and retry.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            creds = await RefreshAsync(creds, ct);
            response = await SendUsageRequestAsync(creds.AccessToken, ct);
        }

        using (response)
        {
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            return Parse(doc.RootElement);
        }
    }

    private Task<HttpResponseMessage> SendUsageRequestAsync(string accessToken, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.TryAddWithoutValidation("anthropic-beta", OAuthBeta);
        req.Headers.Accept.ParseAdd("application/json");
        return _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private async Task<OAuthCredentials> RefreshAsync(OAuthCredentials creds, CancellationToken ct)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = OAuthClientId,
            ["refresh_token"] = creds.RefreshToken,
        });

        using var resp = await _http.PostAsync(TokenUrl, form, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Token refresh failed ({(int)resp.StatusCode}). Re-authenticate with Claude Code.");

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("refresh response missing access_token");
        var refreshToken = root.TryGetProperty("refresh_token", out var rt) && rt.GetString() is { } s
            ? s : creds.RefreshToken;
        var expiresIn = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt64() : 36_000;

        var updated = new OAuthCredentials
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + expiresIn * 1000,
        };
        _store.Write(updated);
        return updated;
    }

    private static UsageSnapshot Parse(JsonElement root)
    {
        return new UsageSnapshot
        {
            FiveHour = ParseWindow(root, "five_hour"),
            SevenDay = ParseWindow(root, "seven_day"),
            SevenDayOpus = ParseWindow(root, "seven_day_opus"),
            SevenDaySonnet = ParseWindow(root, "seven_day_sonnet"),
            ExtraUsageEnabled = root.TryGetProperty("extra_usage", out var eu)
                && eu.ValueKind == JsonValueKind.Object
                && eu.TryGetProperty("is_enabled", out var en)
                && en.ValueKind == JsonValueKind.True,
        };
    }

    private static UsageWindow? ParseWindow(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var w) || w.ValueKind != JsonValueKind.Object)
            return null;

        double util = w.TryGetProperty("utilization", out var u)
            && u.ValueKind == JsonValueKind.Number ? u.GetDouble() : 0;

        DateTimeOffset? resets = null;
        if (w.TryGetProperty("resets_at", out var r) && r.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(r.GetString(), out var dt))
            resets = dt;

        return new UsageWindow(util, resets);
    }

    public void Dispose() => _http.Dispose();
}
