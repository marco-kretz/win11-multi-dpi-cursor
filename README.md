# Multi-DPI Cursor

A [Windhawk](https://windhawk.net/) mod that keeps the mouse cursor the same visual size across monitors with different display scaling on Windows 11.

## The problem

Windows uses a single cursor size for all monitors, in pixels. On a mixed setup (e.g. one monitor at 100 % and one at 125 % scaling), the higher-DPI monitor has denser pixels, so the cursor looks noticeably smaller there than on the 100 % monitor. There is no built-in per-monitor cursor size.

## What it does

The 100 % size is the reference (setting `Base cursor size`, default: 32 px, the Windows default). Whenever the cursor moves to another monitor, the mod reads that monitor's scaling and sets the cursor size accordingly:

```
size = baseSize × monitorDpi / 96
```

At 125 % (120 DPI) a base size of 32 becomes 40, so the cursor looks the same size on both screens.

## Install

1. Install [Windhawk](https://windhawk.net/).
2. In Windhawk, click **Create a new mod**, replace the code with the contents of [`multi-dpi-cursor.wh.cpp`](multi-dpi-cursor.wh.cpp), then **Compile** and **Enable**.

Disabling the mod resets the cursor to the base size. The size is applied live only and never written to the registry, so your saved cursor size from Settings is back after the next sign-in.

## How it works

- Runs as a Windhawk [tool mod](https://github.com/ramensoftware/windhawk/wiki/Mods-as-tools:-Running-mods-in-a-dedicated-process) in its own `windhawk.exe` process; nothing is injected into other processes.
- The window thread is per-monitor DPI aware, otherwise Windows reports 96 DPI for every monitor.
- Mouse movement is received via **Raw Input** (`RegisterRawInputDevices` with `RIDEV_INPUTSINK`) on a hidden window. There is no polling: while the mouse is idle, the mod does nothing. Unlike a low-level mouse hook, raw input is delivered asynchronously, so the mod can never delay mouse input.
- On each movement it checks the monitor under the cursor (`GetCursorPos` + `MonitorFromPoint`). If the monitor hasn't changed, it stops there.
- On a monitor change it reads the DPI (`GetDpiForMonitor`) and sets the cursor size via `SystemParametersInfo(0x2029)`. This is the **undocumented** call the Settings app uses for "Mouse pointer size"; arbitrary sizes (not only the Settings slider steps) are accepted.

## Recommended: LittleBigMouse

I highly recommend [LittleBigMouse](https://github.com/mgth/littlebigmouse) for multi-monitor setups in general. It makes the cursor cross between monitors at the physically matching position, so it enters and leaves screens at the same spot. I use it alongside this mod, and it's where the idea came from: it handles the cursor *position*, but the cursor *size* still differs on monitors with different scaling. Multi-DPI Cursor fills that gap.

## Caveats

- **Only tested on my own machine.** Windows 11 with two monitors (100 % and 125 %). Other setups (more monitors, other scaling values, custom cursor schemes) are untested.
- Relies on an undocumented `SystemParametersInfo` action, which a Windows update could change.
- Changing a monitor's scaling while the mod runs only takes effect after the next monitor switch.
