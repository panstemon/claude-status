using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeStatus;

/// <summary>
/// Reads and writes the Claude Code OAuth credentials file
/// (<c>%USERPROFILE%\.claude\.credentials.json</c>), preserving any fields we
/// don't manage so Claude Code keeps working against the same file.
/// </summary>
public sealed class CredentialStore
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", ".credentials.json");

    private readonly string _path;

    public CredentialStore(string? path = null) => _path = path ?? DefaultPath;

    public bool Exists => File.Exists(_path);

    public OAuthCredentials Read()
    {
        var json = File.ReadAllText(_path);
        var root = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidDataException("credentials file is not a JSON object");
        var oauth = root["claudeAiOauth"] as JsonObject
            ?? throw new InvalidDataException("credentials file is missing 'claudeAiOauth'");

        return new OAuthCredentials
        {
            AccessToken = (string?)oauth["accessToken"]
                ?? throw new InvalidDataException("missing accessToken"),
            RefreshToken = (string?)oauth["refreshToken"]
                ?? throw new InvalidDataException("missing refreshToken"),
            ExpiresAt = (long?)oauth["expiresAt"] ?? 0,
        };
    }

    /// <summary>
    /// Updates only the token fields in place, leaving subscriptionType, scopes,
    /// rateLimitTier, etc. untouched, then writes the file back atomically.
    /// </summary>
    public void Write(OAuthCredentials creds)
    {
        var root = (JsonNode.Parse(File.ReadAllText(_path)) as JsonObject) ?? new JsonObject();
        var oauth = root["claudeAiOauth"] as JsonObject;
        if (oauth is null)
        {
            oauth = new JsonObject();
            root["claudeAiOauth"] = oauth;
        }

        oauth["accessToken"] = creds.AccessToken;
        oauth["refreshToken"] = creds.RefreshToken;
        oauth["expiresAt"] = creds.ExpiresAt;

        var options = new JsonSerializerOptions { WriteIndented = true };
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, root.ToJsonString(options));
        File.Move(tmp, _path, overwrite: true);
    }
}
