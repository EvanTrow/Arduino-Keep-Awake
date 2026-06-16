using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Keep_Awake;

public sealed partial class MainWindow : Window
{
    private readonly ArduinoManager              _arduino = new();
    private readonly AppSettings                 _settings;
    private readonly DispatcherQueueTimer        _reconnectTimer;
    private readonly DispatcherQueueTimer        _scheduleTimer;
    private readonly ObservableCollection<object> _scheduleItems = new();

    private static readonly (string Label, int Seconds)[] IntervalOptions =
    [
        ("1 second",    1),
        ("3 seconds",   3),
        ("5 seconds",   5),
        ("15 seconds",  15),
        ("30 seconds",  30),
        ("1 minute",    60),
        ("2 minutes",   120),
        ("3 minutes",   180),
        ("5 minutes",   300),
        ("10 minutes",  600),
        ("15 minutes",  900),
        ("30 minutes",  1800),
        ("1 hour",      3600),
        ("2 hours",     7200),
        ("3 hours",     10800),
        ("6 hours",     21600),
        ("8 hours",     28800),
        ("12 hours",    43200),
    ];

    private static readonly string[] ScheduleDays =
        ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    private static readonly string[] ScheduleTimes = Enumerable
        .Range(0, 96)
        .Select(i =>
        {
            int h = i / 4, m = (i % 4) * 15;
            string ampm = h < 12 ? "AM" : "PM";
            int h12 = h % 12; if (h12 == 0) h12 = 12;
            return $"{h12}:{m:D2} {ampm}";
        })
        .ToArray();

    private NativeMethods.WndProc? _minSizeWndProc;
    private IntPtr                 _oldWndProc;

    private const int MinWindowWidth  = 580;
    private const int MinWindowHeight = 650;

    private bool _suppressIntervalEvents = false;
    private bool _suppressModeEvents     = false;
    private bool _suppressKeyEvents      = false;
    private bool _initialized            = false;
    private bool _forceClose             = false;
    private bool _enabled                = false;
    private byte _currentKeyCode         = 0xF2; // KEY_F15

    public MainWindow()
    {
        InitializeComponent();

        ScheduleExpander.ItemsSource = _scheduleItems;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.Resize(new SizeInt32(MinWindowWidth, MinWindowHeight));
        AppWindow.Closing += OnAppWindowClosing;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _minSizeWndProc = MinSizeWndProc;
        _oldWndProc = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWLP_WNDPROC);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWLP_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_minSizeWndProc));

        // Populate static combo boxes
        foreach (var (label, _) in IntervalOptions)
            IntervalComboBox.Items.Add(label);

        _suppressKeyEvents = true;
        foreach (var info in KeyMapper.GetAll())
            KeyComboBox.Items.Add(info.DisplayName);
        _suppressKeyEvents = false;

        _settings = AppSettings.Load();
        ApplyLocalSettings();

        var queue = DispatcherQueue.GetForCurrentThread();

        _reconnectTimer          = queue.CreateTimer();
        _reconnectTimer.Interval = TimeSpan.FromSeconds(5);
        _reconnectTimer.Tick    += async (_, _) =>
        {
            if (!_arduino.IsConnected)
                await StartDetectionAsync();
        };

        _scheduleTimer          = queue.CreateTimer();
        _scheduleTimer.Interval = TimeSpan.FromSeconds(30);
        _scheduleTimer.Tick    += (_, _) => EvaluateSchedule();
        _scheduleTimer.Start();

        _arduino.ConnectionChanged += Arduino_ConnectionChanged;
        _arduino.LineReceived      += Arduino_LineReceived;

        _initialized = true;

        Activated += async (_, _) =>
        {
            if (!_arduino.IsConnected)
                await StartDetectionAsync();
        };
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private IntPtr MinSizeWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_GETMINMAXINFO)
        {
            uint dpi  = NativeMethods.GetDpiForWindow(hWnd);
            int  minW = (int)(MinWindowWidth  * dpi / 96.0);
            int  minH = (int)(MinWindowHeight * dpi / 96.0);
            var  mmi  = Marshal.PtrToStructure<NativeMethods.MINMAXINFO>(lParam);
            mmi.ptMinTrackSize.X = minW;
            mmi.ptMinTrackSize.Y = minH;
            Marshal.StructureToPtr(mmi, lParam, false);
            return IntPtr.Zero;
        }
        return NativeMethods.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs e)
    {
        if (!_forceClose && _settings.MinimizeOnClose)
        {
            e.Cancel = true;
            AppWindow.IsShownInSwitchers = false;
            AppWindow.Hide();
            return;
        }
        _reconnectTimer.Stop();
        _arduino.Dispose();
    }

    // ── Arduino detection ──────────────────────────────────────────────────────

    private async Task StartDetectionAsync()
    {
        SetConnectedState(false, "Scanning COM ports…");

        var progress = new Progress<string>(msg => ConnectionInfoBar.Message = msg);
        bool found = await _arduino.FindAndConnectAsync(progress);

        if (!found)
        {
            SetConnectedState(false, "Arduino not found – retrying…");
            _reconnectTimer.Start();
        }
    }

    // ── Arduino event handlers ─────────────────────────────────────────────────

    private void Arduino_ConnectionChanged(object? sender, bool connected)
    {
        if (connected)
        {
            _reconnectTimer.Stop();
            SetConnectedState(true, _arduino.PortName ?? "");
            _arduino.Send("getSettings");
        }
        else
        {
            SetConnectedState(false, "Disconnected");
            _reconnectTimer.Start();
        }
    }

    private void Arduino_LineReceived(object? sender, string line)
    {
        if (line.StartsWith("SETTINGS:"))
        {
            ParseAndApplyArduinoSettings(line);
            return;
        }

        if (line == "Key Press")
        {
            ActivityLabel.Text = $"Key pressed at {DateTime.Now:HH:mm:ss}";
            return;
        }

        ActivityLabel.Text = line;
    }

    private void ParseAndApplyArduinoSettings(string line)
    {
        try
        {
            var body  = line.Substring("SETTINGS:".Length);
            var parts = body.Split(',');
            var dict  = parts
                .Select(p => p.Split('='))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

            bool en  = dict.TryGetValue("enabled", out var ev) && ev == "1";
            int  ms  = dict.TryGetValue("delay",   out var dv) && int.TryParse(dv, out int d) ? d : 60000;
            byte key = dict.TryGetValue("key",     out var kv) && byte.TryParse(kv, out byte k) ? k : (byte)0xD0;

            ApplyArduinoState(en, ms, key);
        }
        catch { }
    }

    private void ApplyArduinoState(bool enabled, int intervalMs, byte keyCode)
    {
        _enabled        = enabled;
        _currentKeyCode = keyCode;

        int secs = Math.Max(1, intervalMs / 1000);

        _suppressIntervalEvents        = true;
        IntervalComboBox.SelectedIndex = FindClosestIntervalIndex(secs);
        _suppressIntervalEvents        = false;

        _suppressKeyEvents = true;
        var all    = KeyMapper.GetAll().ToList();
        int keyIdx = all.FindIndex(k => k.ArduinoCode == keyCode);
        if (keyIdx >= 0) KeyComboBox.SelectedIndex = keyIdx;
        _suppressKeyEvents = false;

        EnableToggle.IsOn = enabled;
        UpdateToggleLabel(enabled);

        var keyInfo = KeyMapper.GetByCode(keyCode);
        UpdateKeyDisplay(keyInfo?.DisplayName ?? $"0x{keyCode:X2}", keyInfo?.IsSafe ?? false);
        UpdateControlsEnabled(true);
        (Application.Current as App)?.UpdateTrayState(true, enabled);
    }

    // ── UI helpers ─────────────────────────────────────────────────────────────

    private void SetConnectedState(bool connected, string portText)
    {
        ConnectionInfoBar.Title    = connected ? $"Connected on {_arduino.PortName}" : "Not connected";
        ConnectionInfoBar.Message  = portText;
        ConnectionInfoBar.Severity = connected ? InfoBarSeverity.Success : InfoBarSeverity.Informational;
        UpdateControlsEnabled(connected);

        if (!connected)
        {
            EnableToggle.IsOn = false;
            UpdateToggleLabel(false);
            (Application.Current as App)?.UpdateTrayState(false, false);
        }
    }

    private void UpdateControlsEnabled(bool connected)
    {
        EnableToggle.IsEnabled     = connected && _settings.Mode == ActiveMode.AlwaysActive;
        IntervalComboBox.IsEnabled = connected;
        KeyComboBox.IsEnabled      = connected;
    }

    private void UpdateToggleLabel(bool enabled)
    {
        KeepAwakeCard.Description = enabled ? "Active" : "Paused";
    }

    private void UpdateKeyDisplay(string name, bool safe)
    {
        KeyExpander.Description = safe
            ? "Safe key – won't interfere with apps"
            : "⚠  May interfere with active applications";
    }

    // ── Toggle ─────────────────────────────────────────────────────────────────

    private void EnableToggle_Toggled(object sender, RoutedEventArgs e)
    {
        bool en = EnableToggle.IsOn;
        if (_enabled == en) return;

        _enabled = en;
        _arduino.Send(en ? "enable" : "disable");
        UpdateToggleLabel(en);
        (Application.Current as App)?.UpdateTrayState(true, en);
    }

    public void ToggleEnabled()
    {
        if (!_arduino.IsConnected) return;
        EnableToggle.IsOn = !EnableToggle.IsOn;
    }

    // ── Interval ───────────────────────────────────────────────────────────────

    private static int FindClosestIntervalIndex(int secs)
    {
        int best = 0, bestDiff = int.MaxValue;
        for (int i = 0; i < IntervalOptions.Length; i++)
        {
            int diff = Math.Abs(IntervalOptions[i].Seconds - secs);
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }
        return best;
    }

    private void IntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _suppressIntervalEvents || IntervalComboBox.SelectedIndex < 0) return;
        SendIntervalToArduino();
    }

    private void SendIntervalToArduino()
    {
        if (IntervalComboBox.SelectedIndex < 0) return;
        int secs = IntervalOptions[IntervalComboBox.SelectedIndex].Seconds;
        _arduino.Send($"setDelay {(long)secs * 1000}");
    }

    // ── Key (inline) ───────────────────────────────────────────────────────────

    private void KeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _suppressKeyEvents || KeyComboBox.SelectedIndex < 0) return;
        var info = KeyMapper.GetAll()[KeyComboBox.SelectedIndex];
        _currentKeyCode = info.ArduinoCode;
        _arduino.Send($"setKey {info.ArduinoCode}");
        UpdateKeyDisplay(info.DisplayName, info.IsSafe);
    }

    // ── Schedule ───────────────────────────────────────────────────────────────

    private void RefreshScheduleList()
    {
        _settings.Schedule = [.. _settings.Schedule.OrderBy(e => e.Day).ThenBy(e => e.Start)];
        _scheduleItems.Clear();

        for (int i = 0; i < _settings.Schedule.Count; i++)
        {
            var entry     = _settings.Schedule[i];
            var idx       = i;
            string header = ScheduleDays[(int)entry.Day];
            string desc   = $"{FormatTime(entry.Start)} – {FormatTime(entry.End)}";

            var editBtn = new Button
            {
                Content = new FontIcon { Glyph = "", FontSize = 14 },
                Padding = new Thickness(8),
            };
            var deleteBtn = new Button
            {
                Content = new FontIcon { Glyph = "", FontSize = 14 },
                Padding = new Thickness(8),
            };

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            btnPanel.Children.Add(editBtn);
            btnPanel.Children.Add(deleteBtn);

            var card = new SettingsCard
            {
                Header      = header,
                Description = desc,
                Content     = btnPanel,
            };

            editBtn.Click += async (_, _) =>
            {
                var updated = await ShowScheduleEntryDialog(entry);
                if (updated == null) return;
                _settings.Schedule[idx] = updated;
                _settings.Save();
                RefreshScheduleList();
                EvaluateSchedule();
            };

            deleteBtn.Click += (_, _) =>
            {
                _settings.Schedule.RemoveAt(idx);
                _settings.Save();
                RefreshScheduleList();
                EvaluateSchedule();
            };

            _scheduleItems.Add(card);
        }
    }

    private async Task<ScheduleEntry?> ShowScheduleEntryDialog(ScheduleEntry? existing)
    {
        var dayBox   = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        var startBox = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        var endBox   = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };

        foreach (var d in ScheduleDays)  dayBox.Items.Add(d);
        foreach (var t in ScheduleTimes) { startBox.Items.Add(t); endBox.Items.Add(t); }

        dayBox.SelectedIndex   = (int)(existing?.Day ?? DayOfWeek.Monday);
        startBox.SelectedIndex = TimeToSlot(existing?.Start ?? new TimeSpan(9,  0, 0));
        endBox.SelectedIndex   = TimeToSlot(existing?.End   ?? new TimeSpan(17, 0, 0));

        var panel = new StackPanel { Spacing = 8, MinWidth = 240 };
        panel.Children.Add(new TextBlock { Text = "Day" });
        panel.Children.Add(dayBox);
        panel.Children.Add(new TextBlock { Text = "Start time", Margin = new Thickness(0, 4, 0, 0) });
        panel.Children.Add(startBox);
        panel.Children.Add(new TextBlock { Text = "End time",   Margin = new Thickness(0, 4, 0, 0) });
        panel.Children.Add(endBox);

        var dialog = new ContentDialog
        {
            Title             = existing == null ? "Add Schedule Entry" : "Edit Schedule Entry",
            Content           = panel,
            PrimaryButtonText = "Save",
            CloseButtonText   = "Cancel",
            DefaultButton     = ContentDialogButton.Primary,
            XamlRoot          = Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;

        return new ScheduleEntry
        {
            Day   = (DayOfWeek)dayBox.SelectedIndex,
            Start = TimeSpan.FromMinutes(startBox.SelectedIndex * 15),
            End   = TimeSpan.FromMinutes(endBox.SelectedIndex   * 15),
        };
    }

    private async void AddScheduleEntry_Click(object sender, RoutedEventArgs e)
    {
        var entry = await ShowScheduleEntryDialog(null);
        if (entry == null) return;
        _settings.Schedule.Add(entry);
        _settings.Save();
        RefreshScheduleList();
        EvaluateSchedule();
    }

    private static string FormatTime(TimeSpan t)
    {
        int h = t.Hours, m = t.Minutes;
        string ampm = h < 12 ? "AM" : "PM";
        int h12 = h % 12; if (h12 == 0) h12 = 12;
        return $"{h12}:{m:D2} {ampm}";
    }

    private static int TimeToSlot(TimeSpan t)
        => Math.Clamp((int)Math.Round(t.TotalMinutes / 15.0) % 96, 0, 95);

    // ── Options ────────────────────────────────────────────────────────────────

    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        _settings.StartWithWindows = StartupToggle.IsOn;
        StartupHelper.SetStartup(_settings.StartWithWindows);
        _settings.Save();
    }

    private void MinimizeOnClose_Changed(object sender, RoutedEventArgs e)
    {
        _settings.MinimizeOnClose = MinimizeOnCloseToggle.IsOn;
        _settings.Save();
    }

    private void RescanButton_Click(object sender, RoutedEventArgs e)
    {
        _reconnectTimer.Stop();
        _arduino.Disconnect();
        _ = StartDetectionAsync();
    }

    // ── Startup settings ───────────────────────────────────────────────────────

    private void ApplyLocalSettings()
    {
        StartupToggle.IsOn         = _settings.StartWithWindows;
        MinimizeOnCloseToggle.IsOn = _settings.MinimizeOnClose;
        RefreshScheduleList();
        ApplyMode(_settings.Mode);
    }

    // ── Mode ───────────────────────────────────────────────────────────────────

    private void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _suppressModeEvents) return;
        var mode = ModeComboBox.SelectedIndex == 1 ? ActiveMode.Scheduled : ActiveMode.AlwaysActive;
        ApplyMode(mode);
        _settings.Save();
    }

    private void ApplyMode(ActiveMode mode)
    {
        _settings.Mode = mode;
        bool scheduled = mode == ActiveMode.Scheduled;

        _suppressModeEvents        = true;
        ModeComboBox.SelectedIndex = scheduled ? 1 : 0;
        _suppressModeEvents        = false;

        KeepAwakeCard.Visibility    = scheduled ? Visibility.Collapsed : Visibility.Visible;
        ScheduleExpander.Visibility = scheduled ? Visibility.Visible   : Visibility.Collapsed;

        UpdateControlsEnabled(_arduino.IsConnected);

        if (scheduled)
            EvaluateSchedule();
    }

    private void EvaluateSchedule()
    {
        if (_settings.Mode != ActiveMode.Scheduled) return;

        bool active = ScheduleManager.IsActiveNow(_settings.Schedule);
        if (!_arduino.IsConnected) return;

        if (_enabled != active)
        {
            _enabled = active;
            _arduino.Send(active ? "enable" : "disable");
            EnableToggle.IsOn = active;
            UpdateToggleLabel(active);
            (Application.Current as App)?.UpdateTrayState(true, active);
        }
    }
}
