using System.Runtime.InteropServices;

// Undocumented SPI used by Settings > Accessibility > Mouse pointer size.
const uint SPI_GETCURSORSIZE = 0x2028, SPI_SETCURSORSIZE = 0x2029;

SetProcessDpiAwarenessContext(-4); // per-monitor v2, otherwise every monitor reports 96 DPI

uint baseSize = args.Length > 0 ? uint.Parse(args[0]) : 32;
Console.WriteLine($"Base cursor size at 100%: {baseSize}. Ctrl+C to quit.");

Console.CancelKeyPress += (_, e) => { e.Cancel = true; SetCursorSize(baseSize); Environment.Exit(0); };

uint lastDpi = 0;
while (true)
{
    GetCursorPos(out var pt);
    GetDpiForMonitor(MonitorFromPoint(pt, 2), 0, out uint dpi, out _);
    if (dpi != lastDpi)
    {
        uint size = (uint)Math.Round(baseSize * dpi / 96.0);
        SetCursorSize(size);
        Console.WriteLine($"DPI {dpi} ({dpi * 100 / 96}%) -> cursor size {size} (now {GetCursorSize()})");
        lastDpi = dpi;
    }
    Thread.Sleep(50); // ponytail: polling, switch to a low-level mouse hook if 50ms latency bothers you
}

static unsafe uint GetCursorSize() { uint s = 0; SystemParametersInfo(SPI_GETCURSORSIZE, 0, (nint)(&s), 0); return s; }
static void SetCursorSize(uint size) => SystemParametersInfo(SPI_SETCURSORSIZE, 0, (nint)size, 0);

[DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(nint value);
[DllImport("user32.dll")] static extern bool GetCursorPos(out POINT pt);
[DllImport("user32.dll")] static extern nint MonitorFromPoint(POINT pt, uint flags);
[DllImport("shcore.dll")] static extern int GetDpiForMonitor(nint hmon, int type, out uint dpiX, out uint dpiY);
[DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint param, nint pv, uint winIni);

struct POINT { public int X, Y; }
