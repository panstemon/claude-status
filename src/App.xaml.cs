using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
        if (_tray is not null)
        {
            _tray.Icon = IconRenderer.Render(snapshot.MaxUtilization);
            _tray.ToolTipText = BuildTooltip(snapshot);
        }
        _flyout?.Update(snapshot);
    }

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

    private void QuitApp()
    {
        _timer?.Stop();
        _tray?.Dispose();
        _client?.Dispose();
        _flyout?.CloseForReal();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
