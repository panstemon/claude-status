using System.Windows.Media;

namespace ClaudeStatus;

/// <summary>Shared green/amber/red palette so the tray badge and flyout agree.</summary>
public static class Severity
{
    public static readonly Color Green = Color.FromRgb(0x3F, 0xB9, 0x50);
    public static readonly Color Amber = Color.FromRgb(0xD2, 0x99, 0x22);
    public static readonly Color Red = Color.FromRgb(0xF8, 0x51, 0x49);
    public static readonly Color Gray = Color.FromRgb(0x6E, 0x76, 0x81);

    public static Color For(double utilization) => utilization switch
    {
        >= 80 => Red,
        >= 50 => Amber,
        _ => Green,
    };
}
