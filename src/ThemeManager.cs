using Microsoft.Win32;

namespace ClaudeStatus;

/// <summary>Reads the Windows light/dark theme flags from the registry.</summary>
public static class ThemeManager
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>App surfaces (windows, menus) follow this flag.</summary>
    public static bool IsLightApp() => ReadFlag("AppsUseLightTheme");

    /// <summary>The taskbar / tray follows this flag.</summary>
    public static bool IsLightSystem() => ReadFlag("SystemUsesLightTheme");

    private static bool ReadFlag(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(name) is int v && v != 0;
        }
        catch
        {
            return false; // assume dark on any failure
        }
    }
}
