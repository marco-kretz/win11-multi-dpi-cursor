using System.Runtime.InteropServices;
using Microsoft.Win32;

static class Program
{
    // Undocumented SPI used by Settings > Accessibility > Mouse pointer size.
    const uint SPI_SETCURSORSIZE = 0x2029;
    const string AppName = "MultiDPICursor", RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    static uint baseSize;
    static nint lastMonitor;

    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\" + AppName, out bool isFirstInstance);
        if (!isFirstInstance) return;

        baseSize = args.Length > 0 ? uint.Parse(args[0]) : 32;
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); // otherwise every monitor reports 96 DPI

        SetPreferredAppMode(1); // AllowDark: native menus follow the system dark mode
        var input = new RawInputWindow(Update);
        AllowDarkModeForWindow(input.Handle, true);

        using var tray = new NotifyIcon
        {
            Icon = Icon.ExtractIcon(Environment.ProcessPath!, 0, SystemInformation.SmallIconSize.Width),
            Text = AppName,
            Visible = true,
        };
        tray.MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) ShowMenu(input.Handle); };
        tray.ShowBalloonTip(3000, AppName, $"Activated. Cursor size now follows monitor scaling (base {baseSize} px).", ToolTipIcon.None);

        Update();
        Application.Run();
        GC.KeepAlive(input);
        SetCursorSize(baseSize);
    }

    static void Update()
    {
        GetCursorPos(out var pt);
        nint monitor = MonitorFromPoint(pt, 2);
        if (monitor == lastMonitor) return; // cheap path for every mouse move on the same screen
        lastMonitor = monitor;

        GetDpiForMonitor(monitor, 0, out uint dpi, out _);
        SetCursorSize((uint)Math.Round(baseSize * dpi / 96.0));
    }

    static void SetCursorSize(uint size) => SystemParametersInfo(SPI_SETCURSORSIZE, 0, (nint)size, 0);

    // Native Win32 menu instead of ContextMenuStrip: gets the Windows 11 look (rounded corners, dark mode).
    static void ShowMenu(nint owner)
    {
        const uint MF_CHECKED = 0x8, MF_SEPARATOR = 0x800, TPM_RIGHTBUTTON = 0x2, TPM_RETURNCMD = 0x100;
        nint menu = CreatePopupMenu();
        AppendMenuW(menu, IsAutostart() ? MF_CHECKED : 0, 1, "Start with Windows");
        AppendMenuW(menu, MF_SEPARATOR, 0, null);
        AppendMenuW(menu, 0, 2, "Exit");

        GetCursorPos(out var pt);
        SetForegroundWindow(owner); // otherwise the menu doesn't close when clicking elsewhere
        int command = TrackPopupMenuEx(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD, pt.X, pt.Y, owner, 0);
        DestroyMenu(menu);

        if (command == 1) SetAutostart(!IsAutostart());
        else if (command == 2) Application.Exit();
    }

    static bool IsAutostart()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) is not null;
    }

    static void SetAutostart(bool enable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enable) key.SetValue(AppName, $"\"{Environment.ProcessPath}\" {baseSize}");
        else key.DeleteValue(AppName, false);
    }

    // Raw input instead of polling: no wakeups while the mouse is idle, and unlike a
    // low-level hook it is delivered asynchronously, so it can never stall mouse input.
    // INPUTSINK needs a real (hidden) window; message-only windows don't receive it reliably.
    sealed class RawInputWindow : NativeWindow
    {
        const int WM_INPUT = 0x00FF;
        const uint RIDEV_INPUTSINK = 0x100;
        readonly Action onMove;

        public RawInputWindow(Action onMove)
        {
            this.onMove = onMove;
            CreateHandle(new CreateParams());
            var rid = new RAWINPUTDEVICE { UsagePage = 1, Usage = 2, Flags = RIDEV_INPUTSINK, Target = Handle }; // generic desktop / mouse
            if (!RegisterRawInputDevices([rid], 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
                throw new InvalidOperationException($"RegisterRawInputDevices failed: {Marshal.GetLastPInvokeError()}");
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_INPUT) onMove();
            base.WndProc(ref m); // DefWindowProc must see WM_INPUT to free the input buffer
        }
    }

    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] static extern nint MonitorFromPoint(POINT pt, uint flags);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(nint hmon, int type, out uint dpiX, out uint dpiY);
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint param, nint pv, uint winIni);
    [DllImport("user32.dll")] static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenuW(nint menu, uint flags, nint id, string? text);
    [DllImport("user32.dll")] static extern int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint tpm);
    [DllImport("user32.dll")] static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(nint hwnd);
    // Undocumented uxtheme exports (by ordinal), used by Explorer, Notepad++ etc. for dark menus.
    [DllImport("uxtheme.dll", EntryPoint = "#135")] static extern int SetPreferredAppMode(int mode);
    [DllImport("uxtheme.dll", EntryPoint = "#133")] static extern bool AllowDarkModeForWindow(nint hwnd, bool allow);
    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

    struct POINT { public int X, Y; }
    struct RAWINPUTDEVICE { public ushort UsagePage, Usage; public uint Flags; public nint Target; }
}
