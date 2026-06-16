using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace Keep_Awake;

/// <summary>
/// Manages a Win32 system-tray icon using Shell_NotifyIcon on a dedicated STA thread.
/// Callbacks are marshalled back to the WinUI DispatcherQueue.
/// </summary>
internal sealed class TrayIconHost : IDisposable
{
    private Thread?                  _thread;
    private volatile IntPtr          _hwnd;
    private volatile IntPtr          _hIcon;
    private volatile bool            _enabled;
    private readonly DispatcherQueue _queue;

    // Keep the delegate alive to prevent GC
    private NativeMethods.WndProc?   _wndProc;

    private const uint TRAY_ID        = 1001;
    private const uint WM_TRAYNOTIFY  = NativeMethods.WM_APP + 1;
    private const uint WM_STOP        = NativeMethods.WM_APP + 2;

    private const string ClassName = "KeepAwakeTrayWnd";

    public Action? ShowWindowRequested;
    public Action? ToggleRequested;
    public Action? ExitRequested;

    public TrayIconHost(DispatcherQueue queue)
    {
        _queue = queue;
    }

    public void Start(bool connected, bool enabled, string tooltip)
    {
        _enabled = enabled;
        _thread = new Thread(state => Run((connected, enabled, tooltip)))
        {
            IsBackground = true,
            Name         = "TrayIconThread",
        };
        _thread.TrySetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public void Update(bool connected, bool enabled, string tooltip)
    {
        _enabled = enabled;
        if (_hwnd == IntPtr.Zero) return;

        var oldIcon = _hIcon;
        _hIcon = CreateIcon(connected, enabled);

        var nid = BuildNID(NativeMethods.NIF_ICON | NativeMethods.NIF_TIP, tooltip);
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref nid);

        if (oldIcon != IntPtr.Zero) NativeMethods.DestroyIcon(oldIcon);
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
            NativeMethods.PostMessage(_hwnd, WM_STOP, IntPtr.Zero, IntPtr.Zero);
    }

    // ── Background thread ──────────────────────────────────────────────────────

    private void Run((bool connected, bool enabled, string tooltip) args)
    {
        _wndProc = WndProcCallback;

        var wc = new NativeMethods.WNDCLASS
        {
            lpfnWndProc   = _wndProc,
            lpszClassName = ClassName,
            hInstance     = NativeMethods.GetModuleHandle(null),
            lpszMenuName  = "",
        };
        NativeMethods.RegisterClass(ref wc);

        _hwnd = NativeMethods.CreateWindowEx(
            0, ClassName, "",
            0, 0, 0, 0, 0,
            new IntPtr(-3), // HWND_MESSAGE
            IntPtr.Zero, NativeMethods.GetModuleHandle(null), IntPtr.Zero);

        _hIcon = CreateIcon(args.connected, args.enabled);
        var nid = BuildNID(NativeMethods.NIF_ICON | NativeMethods.NIF_MESSAGE | NativeMethods.NIF_TIP, args.tooltip);
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref nid);

        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0))
        {
            if (msg.message == WM_STOP) break;
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        var del = BuildNID(0, "");
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref del);
        if (_hIcon != IntPtr.Zero) NativeMethods.DestroyIcon(_hIcon);
        if (_hwnd  != IntPtr.Zero) NativeMethods.DestroyWindow(_hwnd);
        _hwnd  = IntPtr.Zero;
        _hIcon = IntPtr.Zero;
    }

    private IntPtr WndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAYNOTIFY)
        {
            uint mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF);
            if (mouseMsg is NativeMethods.WM_LBUTTONUP or NativeMethods.WM_LBUTTONDBLCLK)
                _queue.TryEnqueue(() => ShowWindowRequested?.Invoke());
            else if (mouseMsg == NativeMethods.WM_RBUTTONUP)
                ShowContextMenu(hWnd);
        }
        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu(IntPtr hWnd)
    {
        NativeMethods.GetCursorPos(out var pt);
        var hMenu = NativeMethods.CreatePopupMenu();

        NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, new IntPtr(1), "Open Settings");
        NativeMethods.AppendMenu(hMenu, NativeMethods.MF_SEPARATOR, IntPtr.Zero, null);
        NativeMethods.AppendMenu(hMenu,
            NativeMethods.MF_STRING | (_enabled ? NativeMethods.MF_CHECKED : 0),
            new IntPtr(2), "Keep Awake Active");
        NativeMethods.AppendMenu(hMenu, NativeMethods.MF_SEPARATOR, IntPtr.Zero, null);
        NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, new IntPtr(3), "Exit");

        NativeMethods.SetForegroundWindow(hWnd);

        uint cmd = NativeMethods.TrackPopupMenu(hMenu,
            NativeMethods.TPM_LEFTALIGN | NativeMethods.TPM_BOTTOMALIGN |
            NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON,
            pt.X, pt.Y, 0, hWnd, IntPtr.Zero);

        NativeMethods.DestroyMenu(hMenu);

        switch (cmd)
        {
            case 1: _queue.TryEnqueue(() => ShowWindowRequested?.Invoke()); break;
            case 2: _queue.TryEnqueue(() => ToggleRequested?.Invoke()); break;
            case 3: _queue.TryEnqueue(() => ExitRequested?.Invoke()); break;
        }
    }

    private NativeMethods.NOTIFYICONDATA BuildNID(uint flags, string tooltip)
        => new()
        {
            cbSize           = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd             = _hwnd,
            uID              = TRAY_ID,
            uFlags           = flags,
            uCallbackMessage = WM_TRAYNOTIFY,
            hIcon            = _hIcon,
            szTip            = tooltip ?? "",
            szInfo           = "",
            szInfoTitle      = "",
        };

    // ── Icon drawing via System.Drawing ───────────────────────────────────────

    private static IntPtr CreateIcon(bool connected, bool enabled)
    {
        using var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        Color bg = !connected ? Color.FromArgb(120, 120, 130) :
                   enabled    ? Color.FromArgb(100, 200, 110) :
                                Color.FromArgb(200, 90, 90);

        using (var brush = new SolidBrush(bg))
            g.FillEllipse(brush, 0, 0, 15, 15);

        using var font = new Font("Segoe UI", 7.5f, System.Drawing.FontStyle.Bold, GraphicsUnit.Point);
        using var tb   = new SolidBrush(Color.White);
        var sf = new StringFormat
        {
            Alignment     = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString("K", font, tb, new RectangleF(0, 0, 16, 16), sf);

        return bmp.GetHicon();
    }
}
