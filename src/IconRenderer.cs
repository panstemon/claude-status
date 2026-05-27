using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using H.NotifyIcon; // BitmapSource.ToStream() (PNG -> ICO)

namespace ClaudeStatus;

/// <summary>
/// Renders the tray icon as a bare percentage number with no background, tinted
/// to match the taskbar theme (white on dark, near-black on light) for maximum
/// contrast. Shows "?" when there is no data.
/// </summary>
public static class IconRenderer
{
    // Rendered at 4x the nominal 16px tray size for crisp downscaling on hi-DPI.
    private const int Size = 64;

    private static readonly Color DarkInk = Color.FromRgb(0x16, 0x18, 0x1C);

    public static System.Drawing.Icon Render(double? utilization)
    {
        string text = utilization is { } u ? FormatPercent(u) : "?";

        var brush = new SolidColorBrush(ThemeManager.IsLightSystem() ? DarkInk : Colors.White);
        brush.Freeze();

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var typeface = new Typeface(new FontFamily("Segoe UI"),
                FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

            // Shrink the font as the number grows so "100" still fits the canvas.
            double fontSize = text.Length >= 3 ? 38 : 52;
            var formatted = new FormattedText(text, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, fontSize, brush, 1.0);

            var origin = new Point(
                (Size - formatted.Width) / 2,
                (Size - formatted.Height) / 2);
            dc.DrawText(formatted, origin);
        }

        var bmp = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();

        // H.NotifyIcon can't consume an in-memory BitmapSource directly, so encode
        // to an .ico stream and wrap it in a GDI+ Icon.
        using var icoStream = bmp.ToStream();
        return new System.Drawing.Icon(icoStream);
    }

    private static string FormatPercent(double u)
    {
        int rounded = (int)Math.Round(u, MidpointRounding.AwayFromZero);
        return Math.Clamp(rounded, 0, 100).ToString(CultureInfo.InvariantCulture);
    }
}
