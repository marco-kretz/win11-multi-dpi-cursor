using System.Runtime.InteropServices;

// Undocumented SPI used by Settings > Accessibility > Mouse pointer size.
const uint SPI_GETCURSORSIZE = 0x2028, SPI_SETCURSORSIZE = 0x2029;
const uint WM_INPUT = 0x00FF, RIDEV_INPUTSINK = 0x100;

SetProcessDpiAwarenessContext(-4); // per-monitor v2, otherwise every monitor reports 96 DPI

uint baseSize = args.Length > 0 ? uint.Parse(args[0]) : 32;
Console.WriteLine($"Base cursor size at 100%: {baseSize}. Ctrl+C to quit.");

Console.CancelKeyPress += (_, e) => { e.Cancel = true; SetCursorSize(baseSize); Environment.Exit(0); };

// Raw input instead of polling: no wakeups while the mouse is idle, and unlike a
// low-level hook it is delivered asynchronously, so it can never stall mouse input.
// INPUTSINK needs a real (hidden) window; message-only windows don't receive it reliably.
nint hwnd = CreateWindowExW(0, "STATIC", null, 0, 0, 0, 0, 0, 0, 0, 0, 0);
var rid = new RAWINPUTDEVICE { UsagePage = 1, Usage = 2, Flags = RIDEV_INPUTSINK, Target = hwnd }; // generic desktop / mouse
if (!RegisterRawInputDevices([rid], 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
    throw new InvalidOperationException($"RegisterRawInputDevices failed: {Marshal.GetLastPInvokeError()}");

nint lastMonitor = 0;
Update();
while (GetMessageW(out var msg, 0, 0, 0) > 0)
{
    if (msg.Message == WM_INPUT) Update();
    DispatchMessageW(ref msg); // DefWindowProc must see WM_INPUT to free the input buffer
}

void Update()
{
    GetCursorPos(out var pt);
    nint monitor = MonitorFromPoint(pt, 2);
    if (monitor == lastMonitor) return; // cheap path for every mouse move on the same screen
    lastMonitor = monitor;

    GetDpiForMonitor(monitor, 0, out uint dpi, out _);
    uint size = (uint)Math.Round(baseSize * dpi / 96.0);
    SetCursorSize(size);
    Console.WriteLine($"DPI {dpi} ({dpi * 100 / 96}%) -> cursor size {size} (now {GetCursorSize()})");
}

static unsafe uint GetCursorSize() { uint s = 0; SystemParametersInfo(SPI_GETCURSORSIZE, 0, (nint)(&s), 0); return s; }
static void SetCursorSize(uint size) => SystemParametersInfo(SPI_SETCURSORSIZE, 0, (nint)size, 0);

[DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(nint value);
[DllImport("user32.dll")] static extern bool GetCursorPos(out POINT pt);
[DllImport("user32.dll")] static extern nint MonitorFromPoint(POINT pt, uint flags);
[DllImport("shcore.dll")] static extern int GetDpiForMonitor(nint hmon, int type, out uint dpiX, out uint dpiY);
[DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint param, nint pv, uint winIni);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern nint CreateWindowExW(uint exStyle, string cls, string? name, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint param);
[DllImport("user32.dll", SetLastError = true)] static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);
[DllImport("user32.dll")] static extern int GetMessageW(out MSG msg, nint hwnd, uint min, uint max);
[DllImport("user32.dll")] static extern nint DispatchMessageW(ref MSG msg);

struct POINT { public int X, Y; }
struct RAWINPUTDEVICE { public ushort UsagePage, Usage; public uint Flags; public nint Target; }
struct MSG { public nint Hwnd; public uint Message; public nint WParam, LParam; public uint Time; public POINT Pt; public uint Private; }
