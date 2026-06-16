using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Keep_Awake;

public partial class App : Application
{
    private MainWindow?     _mainWindow;
    private TrayIconHost?   _tray;
    private DispatcherQueue? _dispatcherQueue;

    private bool _enabled;
    private bool _connected;

    public App()
    {
        InitializeComponent();
    }

    public void UpdateTrayState(bool connected, bool enabled)
    {
        _connected = connected;
        _enabled   = enabled;

        string tip = connected
            ? $"Keep Awake – {(enabled ? "Active" : "Paused")}"
            : "Keep Awake – Disconnected";

        _tray?.Update(connected, enabled, tip);
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        var instance = AppInstance.FindOrRegisterForKey("KeepAwake_SingleInstance");
        if (!instance.IsCurrent)
        {
            var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            await instance.RedirectActivationToAsync(activatedArgs);
            System.Diagnostics.Process.GetCurrentProcess().Kill();
            return;
        }

        instance.Activated += (_, _) => _dispatcherQueue?.TryEnqueue(ShowWindow);

        _tray = new TrayIconHost(_dispatcherQueue!)
        {
            ShowWindowRequested = ShowWindow,
            ToggleRequested     = () => _mainWindow?.ToggleEnabled(),
            ExitRequested       = ExitApp,
        };
        _tray.Start(connected: false, enabled: false, tooltip: "Keep Awake – Disconnected");

        _mainWindow = new MainWindow();

        string[] cmdArgs = Environment.GetCommandLineArgs();
        if (!cmdArgs.Contains("--minimized"))
        {
            _mainWindow.AppWindow.IsShownInSwitchers = true;
            _mainWindow.Activate();
        }
    }

    private void ShowWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.AppWindow.IsShownInSwitchers = true;
        _mainWindow.AppWindow.Show();
        _mainWindow.Activate();
    }

    internal void ExitApp()
    {
        _tray?.Dispose();
        _tray = null;
        _mainWindow?.ForceClose();
    }
}
