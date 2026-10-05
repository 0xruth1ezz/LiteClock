using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Win32;
using Windows.Foundation;
using Windows.Graphics;

namespace LiteClock;

public sealed partial class ClockApp : Application
{
    public static string ExePath => Environment.ProcessPath;
    public ClockWindow Clock { get; private set; }
    public SettingsWindow Editor { get; private set; }
    public bool IsShuttingDown { get; private set; }
    TrayIcon tray;
    Window trayWindow, calendar;
    EventWaitHandle settingsEvent, exitEvent;
    RegisteredWaitHandle settingsWait, exitWait;
    readonly string[] arguments;
    public ClockApp(string[] args)
    {
        arguments = args; InitializeComponent();
        UnhandledException += (_, e) => { Store.Log(e.Exception); };
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Clock = new ClockWindow(this, Store.Load());
        Clock.AppWindow.Show(false);
        var queue = DispatcherQueue.GetForCurrentThread();
        settingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.Channel("ShowSettings"));
        exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.Channel("Exit"));
        settingsWait = ThreadPool.RegisterWaitForSingleObject(settingsEvent, (_, _) => queue.TryEnqueue(ShowSettings), null, -1, false);
        exitWait = ThreadPool.RegisterWaitForSingleObject(exitEvent, (_, _) => queue.TryEnqueue(Shutdown), null, -1, false);
        tray = new TrayIcon(Native.Handle(Clock));
        if (Store.RecoveryMessage != null) tray.Notify(Store.RecoveryMessage);
        if (arguments.Contains("--settings")) ShowSettings();
        if (arguments.Contains("--ui-test")) RunUiTests();
    }
    async void RunUiTests()
    {
        string report = Program.ArgumentAfter(arguments, "--ui-test") ?? Path.Combine(Store.DirectoryPath, "ui-test-results.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report)));
        File.WriteAllText(report, "");
        try
        {
            ShowSettings();
            await System.Threading.Tasks.Task.Delay(600);
            int result = NumberInputTests.Run(report);
            if (result == 0)
            {
                await WinUiTests.Run(this, report);
                await ClockBorderTests.Run(this, report);
                await ScreenColorPickerTests.Run(this, report);
                var baseline = Clock.Config.Copy();
                var draft = baseline.Copy(); draft.Width = 123; draft.BackgroundOpacity = 35;
                draft.Lines[0].Custom = true; draft.Lines[0].Color = "#123456";
                Clock.Apply(draft);
                if (Clock.Config.Width != 123 || Clock.View.DisplayText.Length == 0) throw new Exception("Clock preview was not applied.");
                draft.ClickThrough = true; Clock.Apply(draft);
                if ((Native.GetWindowLong(Native.Handle(Clock), -20) & 0x80020) != 0x80020) throw new Exception("Click-through window styles were not applied.");
                Clock.Apply(baseline);
                ShowTrayMenu(); await System.Threading.Tasks.Task.Delay(150);
                if (trayWindow == null) throw new Exception("WinUI tray menu did not open.");
                Editor.Activate(); await System.Threading.Tasks.Task.Delay(100);
                draft.ClickThrough = false; Clock.Apply(draft);
                Editor.Close();
                if (Clock.Config.Width != baseline.Width) throw new Exception("Cancel did not restore the baseline.");
                if ((Native.GetWindowLong(Native.Handle(Clock), -20) & 0x80020) != 0) throw new Exception("Click-through styles were not cleared.");
                ShowCalendar(); calendar.Close(); calendar = null;
                File.AppendAllText(report, "PASS: WinUI settings, shared clock rendering, click-through styles, tray menu, cancel and calendar lifecycle\n");
            }
            Environment.ExitCode = result;
        }
        catch (Exception ex) { File.AppendAllText(report, ex + "\n"); Environment.ExitCode = 1; }
        finally { Shutdown(); }
    }
    public void HandleNativeMessage(uint message, IntPtr lParam)
    {
        if (message == TrayIcon.TaskbarCreated) tray?.Restore();
        if (message != TrayIcon.CallbackMessage) return;
        int mouseMessage = (int)(lParam.ToInt64() & 0xffff);
        if (mouseMessage == 0x203 || mouseMessage == 0x400) ShowSettings();
        if (mouseMessage == 0x205 || mouseMessage == 0x7B) ShowTrayMenu();
    }
    public void ShowSettings()
    {
        if (IsShuttingDown) return;
        if (Editor == null)
        {
            Editor = new SettingsWindow(this);
            Editor.Closed += (_, _) => Editor = null;
        }
        ((OverlappedPresenter)Editor.AppWindow.Presenter).Restore();
        Editor.Activate(); Native.SetForegroundWindow(Native.Handle(Editor));
    }
    public MenuFlyout CreateMenu()
    {
        var menu = new MenuFlyout();
        Add(menu.Items, "设置 / 调整宽度…", ShowSettings);
        var widths = new MenuFlyoutSubItem { Text = "快速设置宽度" };
        foreach (int value in new[] { 48, 64, 68, 80, 100, 120, 160, 240 })
        {
            int width = value; Add(widths.Items, width.ToString(), () => Clock.ChangeWidth(width));
        }
        menu.Items.Add(widths);
        Add(menu.Items, "回到初始位置", () => Clock.ResetPosition());
        menu.Items.Add(new MenuFlyoutSeparator());
        Add(menu.Items, "退出轻时钟", Shutdown);
        return menu;
    }
    static void Add(IList<MenuFlyoutItemBase> items, string text, Action action)
    {
        var item = new MenuFlyoutItem { Text = text }; item.Click += (_, _) => action(); items.Add(item);
    }
    void ShowTrayMenu()
    {
        if (trayWindow != null) { trayWindow.Activate(); return; }
        var window = new Window { Title = AppDetails.WindowTitle("托盘菜单"), SystemBackdrop = new TransparentBackdrop() };
        var anchor = new Grid(); window.Content = anchor;
        ((OverlappedPresenter)window.AppWindow.Presenter).IsAlwaysOnTop = true;
        Native.ToolWindow(window);
        Native.GetCursorPos(out var cursor);
        var monitor = Native.Monitors().FirstOrDefault(m => cursor.X >= m.Bounds.Left && cursor.X < m.Bounds.Right && cursor.Y >= m.Bounds.Top && cursor.Y < m.Bounds.Bottom) ?? Native.FindMonitor("");
        int w = (int)(290 * monitor.Scale), h = (int)(240 * monitor.Scale);
        window.AppWindow.MoveAndResize(new RectInt32(Math.Clamp(cursor.X - w, monitor.Work.Left, Math.Max(monitor.Work.Left, monitor.Work.Right - w)), Math.Clamp(cursor.Y - h, monitor.Work.Top, Math.Max(monitor.Work.Top, monitor.Work.Bottom - h)), w, h));
        trayWindow = window; var menu = CreateMenu(); bool closed = false;
        void CloseMenu() { if (closed) return; closed = true; menu.Hide(); window.Close(); trayWindow = null; }
        menu.Closed += (_, _) => CloseMenu();
        window.Activated += (_, e) => { if (e.WindowActivationState == WindowActivationState.Deactivated) CloseMenu(); };
        anchor.Loaded += (_, _) => menu.ShowAt(anchor, new FlyoutShowOptions { Position = new Point(0, 0), Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft });
        window.Activate(); Native.SetForegroundWindow(Native.Handle(window));
    }
    public void ShowCalendar()
    {
        if (calendar == null)
        {
            var date = new DateTimeOffset(Clock.Config.DisplayTime(DateTime.UtcNow).Date);
            var view = new CalendarView { Margin = new Thickness(16) };
            // Time offsets and configured zones may cross the built-in range edges.
            if (date < view.MinDate) view.MinDate = date;
            if (date > view.MaxDate) view.MaxDate = date;
            view.SetDisplayDate(date); view.SelectedDates.Add(date);
            calendar = new Window { Title = AppDetails.WindowTitle("日历"), Content = view };
            Native.Center(calendar, 400, 460);
            ((OverlappedPresenter)calendar.AppWindow.Presenter).IsAlwaysOnTop = Clock.Config.AlwaysOnTop;
            calendar.Closed += (_, _) => calendar = null;
        }
        calendar.Activate();
    }
    public static bool StartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key != null && string.Equals(key.GetValue("LiteClock") as string, "\"" + ExePath + "\"", StringComparison.OrdinalIgnoreCase);
    }
    public static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("LiteClock", "\"" + ExePath + "\"");
        else key.DeleteValue("LiteClock", false);
    }
    public async void ShowError(string message)
    {
        try
        {
            ShowSettings();
            if (Editor?.Content is not FrameworkElement root) return;
            if (root.XamlRoot == null)
            {
                var loaded = new System.Threading.Tasks.TaskCompletionSource();
                RoutedEventHandler handler = null;
                handler = (_, _) => { root.Loaded -= handler; loaded.TrySetResult(); };
                root.Loaded += handler; await loaded.Task;
            }
            if (IsShuttingDown || Editor == null) return;
            await new ContentDialog { Title = "轻时钟", Content = message, CloseButtonText = "知道了", XamlRoot = root.XamlRoot }.ShowAsync();
        }
        catch (Exception ex) { Store.Log(ex); }
    }
    public void Shutdown()
    {
        if (IsShuttingDown) return;
        IsShuttingDown = true;
        settingsWait?.Unregister(null); exitWait?.Unregister(null);
        settingsEvent?.Dispose(); exitEvent?.Dispose(); tray?.Dispose();
        trayWindow?.Close(); calendar?.Close(); Editor?.Close(); Clock?.Stop(); Clock?.Close();
        Exit();
    }
}
