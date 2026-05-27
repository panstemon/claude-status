using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using H.NotifyIcon; // BitmapSource.ToStream() (PNG -> ICO)

namespace ClaudeStatus;

/// <summary>
/// Renders the tray icon as a colored rounded badge showing the most-constrained
/// utilization percentage. Color encodes severity: green / amber / red.
/// </summary>
public static class IconRenderer
{
    // Rendered at 4x the nominal 16px tray size for crisp downscaling on hi-DPI.
    private const int Size = 64;

    /// <summary>
    /// Build a tray badge for the given utilization (0-100), or null for "no data".
    /// Returns a System.Drawing.Icon (the only icon type H.NotifyIcon accepts for
    /// dynamically generated icons).
    /// </summary>
    public static System.Drawing.Icon Render(double? utilization)
    {
        var (fill, text) = utilization is { } u
            ? (Severity.For(u), FormatPercent(u))
            : (Severity.Gray, "?");

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var rect = new Rect(1, 1, Size - 2, Size - 2);
            dc.DrawRoundedRectangle(new SolidColorBrush(fill), null, rect, 13, 13);

            var typeface = new Typeface(new FontFamily("Segoe UI"),
                FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

            // Solid dark numerals: high contrast on the clay/red fill and crisp
            // even when the icon is shown at 16px in the tray.
            var ink = new SolidColorBrush(Color.FromRgb(0x16, 0x18, 0x1C));
            ink.Freeze();

            // Shrink the font as the number grows so "100" still fits.
            double fontSize = text.Length >= 3 ? 36 : 48;
            var formatted = new FormattedText(text, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, fontSize, ink, 1.0);

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
