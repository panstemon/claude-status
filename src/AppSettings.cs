using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeStatus;

/// <summary>Which limit window the tray badge number reflects.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BadgeSource
{
    /// <summary>The most-constrained window across all of them.</summary>
    Highest,
    FiveHour,
    Weekly,
}

/// <summary>User-tunable settings persisted to %APPDATA%\ClaudeStatus\settings.json.</summary>
public sealed class AppSettings
{
    public int PollIntervalSeconds { get; set; } = 60;
    public BadgeSource BadgeSource { get; set; } = BadgeSource.Highest;

    [JsonIgnore]
    public static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeStatus");

    [JsonIgnore]
    public static string FilePath => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath))
                    ?? new AppSettings();
        }
        catch { /* fall back to defaults on any read/parse error */ }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
