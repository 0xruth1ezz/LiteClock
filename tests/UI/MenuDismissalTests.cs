using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LiteClock;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;

static class MenuDismissalTests
{
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Native.Point point);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    public static async Task Run(ClockApp app, string report)
    {
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem { Text = "Menu dismissal test" });
        using var dismissal = new MenuDismissal(app.Clock, menu);
        void Check(bool condition, string label)
        {
            if (!condition) throw new Exception("FAIL: " + label);
            File.AppendAllText(report, "PASS: " + label + "\n");
        }
        try
        {
            Check(ToolTipService.GetToolTip(app.Clock.View) == null, "clock has no hover tooltip");
            menu.ShowAt(app.Clock.View, new FlyoutShowOptions { Position = new Point(0, 0) });
            await Task.Delay(200);
            Check(menu.IsOpen && dismissal.IsListening, "outside click detection starts when a clock menu opens");

            Native.GetWindowRect(Native.Handle(app.Clock), out var bounds);
            dismissal.PointerDown(new Native.Point { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 });
            await Task.Delay(50);
            Check(menu.IsOpen, "click detection exempts the clock and its application windows");

            // Exercise real cross-process hit testing, even if the foreground
            // window never changes. No synthetic click is sent to another app.
            Native.Point? outside = null;
            foreach (var monitor in Native.Monitors())
                foreach (var point in new[] {
                    new Native.Point { X = monitor.Bounds.Left + 2, Y = monitor.Bounds.Top + 2 },
                    new Native.Point { X = monitor.Bounds.Right - 2, Y = monitor.Bounds.Top + 2 },
                    new Native.Point { X = monitor.Bounds.Left + 2, Y = monitor.Bounds.Bottom - 2 } })
                {
                    GetWindowThreadProcessId(WindowFromPoint(point), out uint processId);
                    if (processId != 0 && processId != (uint)Environment.ProcessId) outside = point;
                }
            Check(outside.HasValue, "external desktop or application point is available for hit testing");
            dismissal.PointerDown(outside.Value);
            await Task.Delay(250);
            Check(!menu.IsOpen && !dismissal.IsListening, "outside click dismisses menu without a focus change and releases the hook");

            menu.ShowAt(app.Clock.View); await Task.Delay(150);
            Check(menu.IsOpen && dismissal.IsListening, "reopening the menu reattaches outside click detection");
            menu.Hide(); await Task.Delay(150);
            Check(!dismissal.IsListening, "normal menu closure also releases outside click detection");
        }
        finally { menu.Hide(); }
    }
}
