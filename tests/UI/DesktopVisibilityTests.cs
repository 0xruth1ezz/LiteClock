using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LiteClock;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// Opt in with --ui-test <report> --desktop-test on an interactive desktop.
// ToggleDesktop affects other apps, so always pair it with a restore in finally.
static class DesktopVisibilityTests
{
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window, uint attribute, out uint value, uint size);

    public static async Task Run(ClockApp app, string report)
    {
        var baseline = app.Clock.Config.Copy();
        var draft = baseline.Copy();
        draft.Width = 160; draft.Height = 90; draft.Right = 160; draft.Bottom = 160;
        draft.Radius = draft.BorderWidth = draft.TextOpacity = 0;
        draft.Background = "#123456"; draft.BackgroundOpacity = draft.WindowOpacity = 100;
        draft.AlwaysOnTop = true; draft.HideFullscreen = true;
        draft.ClickThrough = draft.HoverEnabled = false;
        draft.RefreshMilliseconds = 100;
        var witness = new Window { Title = "LiteClock desktop visibility test", Content = new TextBlock { Text = "Desktop visibility test" } };
        Native.Center(witness, 360, 180);
        object shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application", true));
        bool showingDesktop = false;
        try
        {
            app.Clock.Apply(draft);
            witness.AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            witness.Activate(); Native.SetForegroundWindow(Native.Handle(witness));
            var elapsed = Stopwatch.StartNew();
            File.AppendAllText(report, "INFO: waiting for foreground focus on the fullscreen test window\n");
            while (Native.GetForegroundWindow() != Native.Handle(witness) && elapsed.ElapsedMilliseconds < 30000) await Task.Delay(100);
            elapsed.Restart();
            while (IsWindowVisible(Native.Handle(app.Clock)) && elapsed.ElapsedMilliseconds < 1800) await Task.Delay(20);
            if (Native.GetForegroundWindow() != Native.Handle(witness)) throw new Exception("The fullscreen test window could not acquire foreground focus.");
            if (IsWindowVisible(Native.Handle(app.Clock))) throw new Exception("Clock did not hide for the foreground fullscreen window.");
            // A periodic time refresh must not mask the delay when returning to desktop.
            draft.RefreshMilliseconds = 60000; app.Clock.Apply(draft);
            Native.GetWindowRect(Native.Handle(app.Clock), out var bounds);
            var sample = new Native.Point { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 };
            var sampleBounds = new Native.Rect { Left = sample.X, Top = sample.Y, Right = sample.X + 1, Bottom = sample.Y + 1 };
            showingDesktop = true;
            shell.GetType().InvokeMember("ToggleDesktop", BindingFlags.InvokeMethod, null, shell, null);
            elapsed.Restart();
            while (Native.IsFullscreen(Native.Handle(app.Clock), Native.FindMonitor(draft.Monitor).Bounds) && elapsed.ElapsedMilliseconds < 1800) await Task.Delay(20);
            if (elapsed.ElapsedMilliseconds >= 1800) throw new Exception("ToggleDesktop did not leave the fullscreen foreground window.");
            elapsed.Restart();
            while (!IsWindowVisible(Native.Handle(app.Clock)) && elapsed.ElapsedMilliseconds < 250) await Task.Delay(16);
            if (!IsWindowVisible(Native.Handle(app.Clock))) throw new Exception("Clock remained hidden after leaving fullscreen for Show Desktop.");
            File.AppendAllText(report, "PASS: fullscreen hiding and return to desktop do not wait for the time refresh\n");
            // Let DWM present the newly shown window before checking pixels.
            await Task.Delay(100);
            elapsed.Restart();
            string firstFailure = null;
            int samples = 0;
            while (elapsed.ElapsedMilliseconds < 1400)
            {
                await Task.Delay(16); ScreenPixels.DwmFlush();
                using var pixels = ScreenPixels.Capture(sampleBounds);
                string actual = ScreenPixels.Hex(pixels.Sample(sample));
                samples++;
                if (actual != "#123456" && firstFailure == null)
                {
                    IntPtr handle = Native.Handle(app.Clock);
                    DwmGetWindowAttribute(handle, 14, out uint cloaked, sizeof(uint));
                    firstFailure = $"{elapsed.ElapsedMilliseconds}ms: pixel {actual}, visible {IsWindowVisible(handle)}, iconic {IsIconic(handle)}, cloaked {cloaked}, style {Native.GetWindowLong(handle, -20):X8}";
                }
            }
            var foregroundClass = new StringBuilder(256);
            GetClassName(Native.GetForegroundWindow(), foregroundClass, foregroundClass.Capacity);
            if (foregroundClass.ToString() != "Progman" && foregroundClass.ToString() != "WorkerW")
                throw new Exception("ToggleDesktop did not put the desktop in the foreground: " + foregroundClass);
            if (firstFailure != null) throw new Exception("Clock disappeared during Show Desktop: " + firstFailure);
            File.AppendAllText(report, $"PASS: clock stays visible throughout Show Desktop ({samples} screen samples, 60-second refresh)\n");
        }
        finally
        {
            if (showingDesktop) shell.GetType().InvokeMember("ToggleDesktop", BindingFlags.InvokeMethod, null, shell, null);
            Marshal.FinalReleaseComObject(shell);
            witness.Close(); app.Clock.Apply(baseline);
        }
    }
}
