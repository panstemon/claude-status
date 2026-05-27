using System.Diagnostics;
using Microsoft.Win32;

namespace ClaudeStatus;

/// <summary>
/// Toggles "launch at login" via the per-user Run key
/// (HKCU\Software\Microsoft\Windows\CurrentVersion\Run). Per-user, so it needs
/// no admin rights and only affects the current account.
/// </summary>
public static class AutostartManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClaudeStatus";

    /// <summary>Path to the running executable, quoted for the registry command line.</summary>
    private static string ExecutablePath => Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? throw new InvalidOperationException("cannot determine executable path");

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, $"\"{ExecutablePath}\"");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static void Set(bool enabled)
    {
        if (enabled) Enable(); else Disable();
    }
}
