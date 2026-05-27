using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace ClaudeStatus;

public partial class FlyoutWindow : Window
{
    private const double TrackWidth = 288; // matches the track Width in XAML

    private readonly ObservableCollection<LimitRow> _rows = new();
    private DateTime _hiddenAt = DateTime.MinValue;

    public FlyoutWindow()
    {
        InitializeComponent();
        RowsList.ItemsSource = _rows;
    }

    /// <summary>Render a fresh snapshot.</summary>
    public void Update(UsageSnapshot snapshot)
    {
        _rows.Clear();
        AddRow("5-hour session", snapshot.FiveHour);
        AddRow("Weekly · all models", snapshot.SevenDay);
        AddRow("Weekly · Opus", snapshot.SevenDayOpus);
        AddRow("Weekly · Sonnet", snapshot.SevenDaySonnet);

        UpdatedText.Text = $"updated {DateTime.Now:HH:mm}";

        if (_rows.Count == 0)
            ShowMessage("No usage windows reported.");
        else
            MessageText.Visibility = Visibility.Collapsed;
    }

    /// <summary>Render an error/empty state.</summary>
    public void ShowError(string message)
    {
        _rows.Clear();
        UpdatedText.Text = $"updated {DateTime.Now:HH:mm}";
        ShowMessage(message);
    }

    private void ShowMessage(string text)
    {
        MessageText.Text = text;
        MessageText.Visibility = Visibility.Visible;
    }

    private void AddRow(string name, UsageWindow? window)
    {
        if (window is null) return;

        double pct = Math.Clamp(window.Utilization, 0, 100);
        double fill = pct <= 0 ? 0 : Math.Max(3, TrackWidth * pct / 100);

        var brush = new SolidColorBrush(Severity.For(pct));
        brush.Freeze();

        _rows.Add(new LimitRow
        {
            Name = name,
            PercentText = $"{Math.Round(pct)}%",
            ResetText = FormatReset(window.ResetsAt),
            FillBrush = brush,
            FillWidth = fill,
        });
    }

    private static string FormatReset(DateTimeOffset? resetsAt)
    {
        if (resetsAt is null) return "no active window";

        var local = resetsAt.Value.ToLocalTime();
        var delta = local - DateTimeOffset.Now;

        if (delta <= TimeSpan.Zero) return "resetting…";
        if (delta < TimeSpan.FromHours(24))
        {
            int h = (int)delta.TotalHours;
            int m = delta.Minutes;
            return h == 0 ? $"resets in {m}m" : $"resets in {h}h {m}m";
        }
        return $"resets {local:MMM d, HH:mm}";
    }

    /// <summary>Toggle visibility, anchored to the tray. Clicking the tray while open closes it.</summary>
    public void Toggle()
    {
        // Clicking the tray icon deactivates (and thus hides) an open flyout just
        // before this runs; if that happened very recently, leave it closed.
        if ((DateTime.UtcNow - _hiddenAt).TotalMilliseconds < 250)
            return;

        ShowNearTray();
    }

    private void ShowNearTray()
    {
        if (!IsVisible) Show();
        UpdateLayout();

        var wa = SystemParameters.WorkArea;

        // Anchor to the tray icon: center horizontally on the cursor, sit just
        // above the taskbar. Fall back to the corner if the cursor is unavailable.
        double anchorX = wa.Right - Width / 2;
        if (GetCursorPos(out var p))
        {
            var src = PresentationSource.FromVisual(this);
            var toDip = src?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            anchorX = toDip.Transform(new Point(p.X, p.Y)).X;
        }

        double left = anchorX - Width / 2;
        left = Math.Max(wa.Left + 4, Math.Min(left, wa.Right - Width - 4));

        Left = left;
        Top = wa.Bottom - ActualHeight + 8;
        Activate();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (IsVisible)
        {
            Hide();
            _hiddenAt = DateTime.UtcNow;
        }
    }

    // A tool window should never really close while the app runs; hide instead.
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_reallyClose)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    private bool _reallyClose;
    public void CloseForReal()
    {
        _reallyClose = true;
        Close();
    }
}

/// <summary>View-model for a single limit row in the flyout.</summary>
public sealed class LimitRow
{
    public string Name { get; init; } = "";
    public string PercentText { get; init; } = "";
    public string ResetText { get; init; } = "";
    public Brush FillBrush { get; init; } = Brushes.Gray;
    public double FillWidth { get; init; }
}
