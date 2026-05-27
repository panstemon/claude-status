using System.Windows.Media;

namespace ClaudeStatus;

/// <summary>Shared palette + severity logic for the tray badge and flyout bars.</summary>
public static class Severity
{
    /// <summary>Claude's clay accent — normal usage.</summary>
    public static readonly Color Clay = Color.FromRgb(0xD9, 0x77, 0x57);

    /// <summary>Critical — running low on a window.</summary>
    public static readonly Color Red = Color.FromRgb(0xF8, 0x51, 0x49);

    /// <summary>No data available.</summary>
    public static readonly Color Gray = Color.FromRgb(0x6E, 0x76, 0x81);

    /// <summary>Clay during normal usage; red once a window gets critical (≥ 80%).</summary>
    public static Color For(double utilization) => utilization >= 80 ? Red : Clay;
}
