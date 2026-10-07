using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace LiteClock;

// Win32 is only used for desktop integration that has no XAML control:
// notification-area registration, monitors, hit testing and window positioning.
public static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct MinMaxInfo { public Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MonitorInfo
    {
        public int Size; public Rect Bounds, Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    public sealed record Monitor(IntPtr Handle, string DeviceName, Rect Bounds, Rect Work, bool Primary)
    {
        public double Scale
        {
            get { return GetDpiForMonitor(Handle, 0, out uint dpi, out _) == 0 ? dpi / 96.0 : 1; }
        }
    }
    delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref Rect bounds, IntPtr data);
    public delegate IntPtr SubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr data);
    public delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int objectId, int childId, uint threadId, uint time);
    [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr window, int index, int value);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr window, uint color, byte alpha, uint flags);
    [DllImport("user32.dll")] public static extern short GetKeyState(int key);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("comctl32.dll")] public static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc proc, UIntPtr id, IntPtr data);
    [DllImport("comctl32.dll")] public static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc proc, UIntPtr id);
    [DllImport("comctl32.dll")] public static extern IntPtr DefSubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int MessageBox(IntPtr hwnd, string text, string caption, uint type);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, uint attribute, ref uint value, uint size);

    public static IReadOnlyList<Monitor> Monitors()
    {
        var list = new List<Monitor>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr handle, IntPtr dc, ref Rect bounds, IntPtr data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(handle, ref info)) list.Add(new Monitor(handle, info.Device, info.Bounds, info.Work, (info.Flags & 1) != 0));
            return true;
        }, IntPtr.Zero);
        return list;
    }
    public static Monitor FindMonitor(string name)
    {
        var monitors = Monitors();
        return monitors.FirstOrDefault(m => m.DeviceName == name) ?? monitors.FirstOrDefault(m => m.Primary) ?? monitors[0];
    }
    public static bool IsFullscreen(IntPtr own, Rect screen)
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero || window == own) return false;
        var name = new StringBuilder(256); GetClassName(window, name, name.Capacity);
        if (new[] { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" }.Contains(name.ToString())) return false;
        return GetWindowRect(window, out var rect) && rect.Left <= screen.Left && rect.Top <= screen.Top && rect.Right >= screen.Right && rect.Bottom >= screen.Bottom;
    }
    public static IntPtr Handle(Window window) => WinRT.Interop.WindowNative.GetWindowHandle(window);
    public static void ExcludeFromPeek(IntPtr window)
    {
        // Desktop widgets should remain visible during the shell's Peek animation.
        uint enabled = 1;
        Marshal.ThrowExceptionForHR(DwmSetWindowAttribute(window, 12, ref enabled, sizeof(uint))); // DWMWA_EXCLUDED_FROM_PEEK
    }
    public static void Icon(Window window) => window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "LiteClock.ico"));
    public static void Center(Window window, int width, int height)
    {
        var monitor = FindMonitor(""); var work = monitor.Work; double scale = monitor.Scale;
        int w = Math.Min((int)(width * scale), work.Width - 32), h = Math.Min((int)(height * scale), work.Height - 32);
        window.AppWindow.MoveAndResize(new RectInt32(work.Left + (work.Width - w) / 2, work.Top + (work.Height - h) / 2, w, h));
        Icon(window);
    }
    public static void ToolWindow(Window window)
    {
        window.AppWindow.IsShownInSwitchers = false;
        var presenter = (OverlappedPresenter)window.AppWindow.Presenter;
        presenter.IsResizable = presenter.IsMaximizable = presenter.IsMinimizable = false;
        presenter.SetBorderAndTitleBar(false, false);
        IntPtr hwnd = Handle(window);
        SetWindowLong(hwnd, -20, (GetWindowLong(hwnd, -20) | 0x80) & ~0x40000);
        RemoveWindowFrame(window);
    }
    public static void RemoveWindowFrame(Window window)
    {
        IntPtr hwnd = Handle(window);
        // Presenter updates can leave WS_DLGFRAME on a non-resizable window,
        // even with HasBorder=false. Strip the native frame and recalculate the
        // client area so the clock's XAML border is the only visible outline.
        int style = GetWindowLong(hwnd, -16);
        int frameless = style & ~0x00C40000; // WS_CAPTION | WS_THICKFRAME
        if (style != frameless)
        {
            SetWindowLong(hwnd, -16, frameless);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x37); // FRAMECHANGED | NOMOVE | NOSIZE | NOZORDER | NOACTIVATE
        }
        // WinUI's presenter hides the traditional frame, but Windows 11 can
        // still draw its own one-pixel outline independently of XAML borders.
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            uint noBorder = 0xFFFFFFFE; // DWMWA_COLOR_NONE
            Marshal.ThrowExceptionForHR(DwmSetWindowAttribute(hwnd, 34, ref noBorder, sizeof(uint)));
        }
    }
    public static void MinimumSize(Window window, int width, int height)
    {
        IntPtr hwnd = Handle(window);
        SubclassProc hook = (IntPtr handle, uint message, IntPtr w, IntPtr l, UIntPtr id, IntPtr data) =>
        {
            if (message == 0x24)
            {
                var limits = Marshal.PtrToStructure<MinMaxInfo>(l);
                double scale = GetDpiForWindow(handle) / 96.0;
                limits.MinTrackSize = new Point { X = (int)(width * scale), Y = (int)(height * scale) };
                Marshal.StructureToPtr(limits, l, false); return IntPtr.Zero;
            }
            return DefSubclassProc(handle, message, w, l);
        };
        SetWindowSubclass(hwnd, hook, (UIntPtr)2, IntPtr.Zero);
        window.Closed += (_, _) => { RemoveWindowSubclass(hwnd, hook, (UIntPtr)2); GC.KeepAlive(hook); };
    }
}
