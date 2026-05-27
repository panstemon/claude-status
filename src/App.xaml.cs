using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using H.NotifyIcon;

namespace ClaudeStatus;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private TaskbarIcon? _tray;
    private FlyoutWindow? _flyout;
    private UsageClient? _client;
    private AppSettings _settings = new();
    private DispatcherTimer? _timer;

    private MenuItem? _autostartItem;
    private readonly List<(MenuItem Item, int Seconds)> _intervalItems = new();
    private readonly List<(MenuItem Item, BadgeSource Source)> _badgeItems = new();
    private UsageSnapshot? _lastSnapshot;
    private ResourceDictionary? _colorTheme;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Only one instance (autostart + a manual launch shouldn't double up).
        _singleInstance = new Mutex(initiallyOwned: true,
            @"Local\ClaudeStatus_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        _settings = AppSettings.Load();
        ApplyColorTheme();
        _client = new UsageClient();
        _flyout = new FlyoutWindow();

        _tray = new TaskbarIcon
        {
            ToolTipText = "Claude usage — loading…",
            Icon = IconRenderer.Render(null),
            ContextMenu = BuildContextMenu(),
            NoLeftClickDelay = true,
        };
        _tray.TrayLeftMouseUp += (_, _) => _flyout?.Toggle();
        _tray.ForceCreate();

        // Re-tint the badge immediately when the user switches light/dark theme.
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(15, _settings.PollIntervalSeconds)),
        };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        _ = RefreshAsync(); // immediate first fetch
    }

    private async Task RefreshAsync()
    {
        if (_client is null) return;
        try
        {
            var snapshot = await _client.GetSnapshotAsync();
            ApplySnapshot(snapshot);
        }
        catch (Exception ex)
        {
            if (_tray is not null)
            {
                _tray.Icon = IconRenderer.Render(null);
                _tray.ToolTipText = "Claude usage — error\n" + Truncate(ex.Message, 100);
            }
            _flyout?.ShowError(ex.Message);
        }
    }

    private void ApplySnapshot(UsageSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        UpdateBadge();
        if (_tray is not null)
            _tray.ToolTipText = BuildTooltip(snapshot);
        _flyout?.Update(snapshot);
    }

    /// <summary>Re-render the tray badge from the last snapshot and the chosen source.</summary>
    private void UpdateBadge()
    {
        if (_tray is null) return;
        double? value = _lastSnapshot is null
            ? null
            : BadgeValue(_lastSnapshot, _settings.BadgeSource);
        _tray.Icon = IconRenderer.Render(value);
    }

    private static double? BadgeValue(UsageSnapshot s, BadgeSource source) => source switch
    {
        BadgeSource.FiveHour => s.FiveHour?.Utilization,
        BadgeSource.Weekly => s.SevenDay?.Utilization,
        _ => s.MaxUtilization,
    };

    private static string BuildTooltip(UsageSnapshot s)
    {
        var parts = new List<string>();
        if (s.FiveHour is not null) parts.Add($"5h: {Math.Round(s.FiveHour.Utilization)}%");
        if (s.SevenDay is not null) parts.Add($"7d: {Math.Round(s.SevenDay.Utilization)}%");
        if (s.SevenDayOpus is not null) parts.Add($"Opus: {Math.Round(s.SevenDayOpus.Utilization)}%");
        return parts.Count == 0 ? "Claude usage" : "Claude usage — " + string.Join(" · ", parts);
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var open = new MenuItem { Header = "Open" };
        open.Click += (_, _) => _flyout?.Toggle();
        menu.Items.Add(open);

        var refresh = new MenuItem { Header = "Refresh now" };
        refresh.Click += async (_, _) => await RefreshAsync();
        menu.Items.Add(refresh);

        menu.Items.Add(new Separator());

        _autostartItem = new MenuItem
        {
            Header = "Start with Windows",
            IsCheckable = true,
            IsChecked = AutostartManager.IsEnabled,
        };
        _autostartItem.Click += (_, _) =>
        {
            try
            {
                AutostartManager.Set(_autostartItem.IsChecked);
            }
            catch
            {
                _autostartItem.IsChecked = AutostartManager.IsEnabled; // revert on failure
            }
        };
        menu.Items.Add(_autostartItem);

        var interval = new MenuItem { Header = "Refresh interval" };
        foreach (var (label, seconds) in new[] { ("30 seconds", 30), ("1 minute", 60), ("5 minutes", 300) })
        {
            var item = new MenuItem
            {
                Header = label,
                IsCheckable = true,
                IsChecked = _settings.PollIntervalSeconds == seconds,
            };
            int captured = seconds;
            item.Click += (_, _) => SetInterval(captured);
            _intervalItems.Add((item, seconds));
            interval.Items.Add(item);
        }
        menu.Items.Add(interval);

        var badge = new MenuItem { Header = "Badge shows" };
        foreach (var (label, src) in new[]
                 {
                     ("Highest", BadgeSource.Highest),
                     ("5-hour session", BadgeSource.FiveHour),
                     ("Weekly", BadgeSource.Weekly),
                 })
        {
            var item = new MenuItem
            {
                Header = label,
                IsCheckable = true,
                IsChecked = _settings.BadgeSource == src,
            };
            var captured = src;
            item.Click += (_, _) => SetBadgeSource(captured);
            _badgeItems.Add((item, src));
            badge.Items.Add(item);
        }
        menu.Items.Add(badge);

        menu.Items.Add(new Separator());

        var quit = new MenuItem { Header = "Quit" };
        quit.Click += (_, _) => QuitApp();
        menu.Items.Add(quit);

        return menu;
    }

    private void SetInterval(int seconds)
    {
        _settings.PollIntervalSeconds = seconds;
        _settings.Save();
        if (_timer is not null) _timer.Interval = TimeSpan.FromSeconds(seconds);
        foreach (var (item, s) in _intervalItems) item.IsChecked = s == seconds;
    }

    private void SetBadgeSource(BadgeSource source)
    {
        _settings.BadgeSource = source;
        _settings.Save();
        foreach (var (item, s) in _badgeItems) item.IsChecked = s == source;
        UpdateBadge();
    }

    private void QuitApp()
    {
        _timer?.Stop();
        _tray?.Dispose();
        _client?.Dispose();
        _flyout?.CloseForReal();
        Shutdown();
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
            Dispatcher.Invoke(() =>
            {
                ApplyColorTheme();
                UpdateBadge();
            });
    }

    /// <summary>Merge the light or dark palette to match the current Windows app theme.</summary>
    private void ApplyColorTheme()
    {
        var file = ThemeManager.IsLightApp() ? "Theme.Light.xaml" : "Theme.Dark.xaml";
        var uri = new Uri($"pack://application:,,,/{file}", UriKind.Absolute);
        var dict = new ResourceDictionary { Source = uri };

        if (_colorTheme is not null)
            Resources.MergedDictionaries.Remove(_colorTheme);
        Resources.MergedDictionaries.Add(dict);
        _colorTheme = dict;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
