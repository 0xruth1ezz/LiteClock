using System;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Input;

namespace LiteClock;

public sealed class ClockWindow : Window
{
    readonly ClockApp app;
    readonly DispatcherTimer timer = new(), clickTimer = new();
    readonly Native.SubclassProc hook;
    readonly IntPtr hwnd;
    bool draggingWidth, draggingPosition, suppressClick, hidden, stopped;
    Native.Point dragOrigin, lastClickPoint;
    Settings dragSettings;
    double scale = 1;
    DateTime lastPress = DateTime.MinValue, lastPosition = DateTime.MinValue;
    Native.Rect bounds;
    public Settings Config { get; private set; }
    public ClockView View { get; } = new();
    public event EventHandler ConfigurationChanged;
    public event EventHandler TimeChanged;
    public ClockWindow(ClockApp owner, Settings settings)
    {
        app = owner; Title = AppDetails.DisplayTitle; Content = View;
        SystemBackdrop = new TransparentBackdrop();
        Native.ToolWindow(this); Native.Icon(this);
        hwnd = Native.Handle(this);
        Native.SetWindowLong(hwnd, -20, Native.GetWindowLong(hwnd, -20) | 0x08000000);
        hook = Hook; Native.SetWindowSubclass(hwnd, hook, (UIntPtr)1, IntPtr.Zero);
        View.ContextFlyout = app.CreateMenu();
        View.PointerPressed += BeginDrag;
        View.PointerMoved += DuringDrag;
        View.PointerReleased += EndDrag;
        View.PointerCaptureLost += (_, _) => FinishDrag();
        View.PointerCanceled += (_, _) => FinishDrag();
        timer.Tick += (_, _) => Tick();
        clickTimer.Interval = TimeSpan.FromMilliseconds(Native.GetDoubleClickTime());
        clickTimer.Tick += (_, _) => { clickTimer.Stop(); DoAction(Config.ClickAction); };
        Closed += (_, _) => { Stop(); if (!app.IsShuttingDown) app.Shutdown(); };
        Apply(settings); timer.Start();
    }
    IntPtr Hook(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr data)
    {
        app.HandleNativeMessage(message, lParam);
        if (message == 0x21) return (IntPtr)3; // MA_NOACTIVATE: clicking the clock keeps app focus.
        if (message == 0x84 && Config?.ClickThrough == true) return (IntPtr)(-1);
        if (message == 0x7E || message == 0x2E0) DispatcherQueue.TryEnqueue(PositionClock);
        return Native.DefSubclassProc(window, message, wParam, lParam);
    }
    public void Apply(Settings settings)
    {
        settings.Validate(); Config = settings.Copy(); View.Apply(Config);
        timer.Interval = TimeSpan.FromMilliseconds(Config.RefreshMilliseconds);
        ((OverlappedPresenter)AppWindow.Presenter).IsAlwaysOnTop = Config.AlwaysOnTop;
        Native.RemoveWindowFrame(this);
        int style = Native.GetWindowLong(hwnd, -20);
        // WS_EX_LAYERED + WS_EX_TRANSPARENT delegates hit testing across processes.
        // The transparent composition backdrop continues to supply per-pixel alpha.
        Native.SetWindowLong(hwnd, -20, Config.ClickThrough ? style | 0x80000 | 0x20 : style & ~(0x80000 | 0x20));
        if (Config.ClickThrough) Native.SetLayeredWindowAttributes(hwnd, 0, 255, 2);
        PositionClock(); ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
    public void PositionClock()
    {
        if (stopped) return;
        var monitor = Native.FindMonitor(Config.Monitor); bounds = monitor.Bounds; scale = monitor.Scale;
        int width = Math.Clamp((int)Math.Round(Config.Width * scale), 1, bounds.Width);
        int height = Math.Clamp((int)Math.Round(Config.Height * scale), 1, bounds.Height);
        int x = Math.Max(bounds.Left, bounds.Right - width - (int)Math.Round(Config.Right * scale));
        int y = Math.Max(bounds.Top, bounds.Bottom - height - (int)Math.Round(Config.Bottom * scale));
        Native.SetWindowPos(hwnd, new IntPtr(Config.AlwaysOnTop ? -1 : -2), x, y, width, height, 0x10 | 0x200);
    }
    void Tick()
    {
        var now = DateTime.UtcNow; View.UpdateTime(now); TimeChanged?.Invoke(this, EventArgs.Empty);
        if ((now - lastPosition).TotalSeconds < 1) return;
        lastPosition = now;
        bool hide = Config.HideFullscreen && app.Editor == null && !draggingPosition && !draggingWidth && Native.IsFullscreen(hwnd, bounds);
        if (hide != hidden)
        {
            hidden = hide;
            if (hide) AppWindow.Hide(); else AppWindow.Show(false);
        }
        if (!draggingWidth && !draggingPosition) PositionClock();
    }
    void BeginDrag(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(View);
        if (point.Properties.IsMiddleButtonPressed) { DoAction(Config.MiddleClickAction); e.Handled = true; return; }
        if (!point.Properties.IsLeftButtonPressed) return;
        Native.GetCursorPos(out var cursor);
        var now = DateTime.UtcNow;
        if ((now - lastPress).TotalMilliseconds <= Native.GetDoubleClickTime() && Math.Abs(cursor.X - lastClickPoint.X) <= 4 * scale && Math.Abs(cursor.Y - lastClickPoint.Y) <= 4 * scale)
        {
            clickTimer.Stop(); suppressClick = true; lastPress = DateTime.MinValue;
            DoAction(Config.DoubleClickAction); e.Handled = true; return;
        }
        lastPress = now; lastClickPoint = cursor;
        if (Config.LockPosition || app.Editor != null) return;
        draggingPosition = (Native.GetKeyState(0x10) & 0x8000) != 0;
        draggingWidth = !draggingPosition && point.Position.X <= 7;
        if (!draggingPosition && !draggingWidth) return;
        dragOrigin = cursor; dragSettings = Config.Copy(); clickTimer.Stop();
        View.CapturePointer(e.Pointer); e.Handled = true;
    }
    void DuringDrag(object sender, PointerRoutedEventArgs e)
    {
        if (!draggingWidth && !draggingPosition) return;
        Native.GetCursorPos(out var point);
        double dx = (point.X - dragOrigin.X) / scale, dy = (point.Y - dragOrigin.Y) / scale;
        if (draggingWidth) Config.Width = Math.Round(Math.Max(24, Math.Min(Math.Min(1200, bounds.Width / scale - Config.Right), dragSettings.Width - dx)));
        if (draggingPosition)
        {
            Config.Right = Math.Round(Math.Max(0, Math.Min(bounds.Width / scale - Config.Width, dragSettings.Right - dx)));
            Config.Bottom = Math.Round(Math.Max(0, Math.Min(bounds.Height / scale - Config.Height, dragSettings.Bottom - dy)));
        }
        PositionClock();
    }
    void EndDrag(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(View).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased) return;
        if (draggingWidth || draggingPosition)
        {
            FinishDrag(); View.ReleasePointerCapture(e.Pointer); lastPress = DateTime.MinValue; e.Handled = true; return;
        }
        if (suppressClick) { suppressClick = false; return; }
        clickTimer.Stop(); clickTimer.Start();
    }
    void FinishDrag()
    {
        if (!draggingWidth && !draggingPosition) return;
        draggingWidth = draggingPosition = false;
        SaveInteractive(); ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
    void DoAction(string action)
    {
        if (action == "Settings") app.ShowSettings();
        else if (action == "Calendar") app.ShowCalendar();
        else if (action == "Copy")
        {
            try { var data = new DataPackage(); data.SetText(View.DisplayText); Clipboard.SetContent(data); Clipboard.Flush(); }
            catch (Exception ex) { Store.Log(ex); }
        }
    }
    void SaveInteractive()
    {
        try { Store.Save(Config); }
        catch (Exception ex) { Store.Log(ex); app.ShowError("未能保存设置：" + ex.Message); }
    }
    public void ChangeWidth(int width)
    {
        if (app.Editor != null) { app.ShowSettings(); return; }
        var value = Config.Copy(); value.Width = width; Apply(value); SaveInteractive();
    }
    public void ResetPosition()
    {
        if (app.Editor != null) { app.ShowSettings(); return; }
        var value = Config.Copy(); value.Right = 0; value.Bottom = 56; value.Monitor = ""; Apply(value); SaveInteractive();
    }
    public void Stop()
    {
        if (stopped) return; stopped = true;
        timer.Stop(); clickTimer.Stop(); Native.RemoveWindowSubclass(hwnd, hook, (UIntPtr)1);
    }
}
