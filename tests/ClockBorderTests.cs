using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiteClock;

static class ClockBorderTests
{
    public static async Task Run(ClockApp app, string report)
    {
        var baseline = app.Clock.Config.Copy();
        var draft = baseline.Copy();
        draft.Width = 160; draft.Height = 90; draft.Right = 100; draft.Bottom = 100;
        draft.Radius = draft.BorderWidth = draft.TextOpacity = 0;
        draft.Background = "#123456"; draft.BorderColor = "#E87632";
        draft.BackgroundOpacity = draft.WindowOpacity = draft.BorderOpacity = 100;
        draft.AlwaysOnTop = true; draft.HideFullscreen = draft.ClickThrough = draft.HoverEnabled = false;
        try
        {
            app.Clock.AppWindow.Show(false);
            await CheckEdges("#123456", "zero border width leaves no system outline");
            draft.BorderWidth = 3;
            await CheckEdges("#E87632", "custom border reaches all four window edges");
            draft.BorderWidth = draft.BorderOpacity = 0;
            await CheckEdges("#123456", "disabling a custom border removes it after live update");
            draft.AlwaysOnTop = false; app.Clock.Apply(draft);
            draft.AlwaysOnTop = true;
            await CheckEdges("#123456", "changing the presenter keeps the clock frameless");
        }
        finally { app.Clock.Apply(baseline); }

        async Task CheckEdges(string expected, string label)
        {
            app.Clock.Apply(draft);
            Native.GetWindowRect(Native.Handle(app.Clock), out var bounds);
            var points = new[]
            {
                new Native.Point { X = (bounds.Left + bounds.Right) / 2, Y = bounds.Top },
                new Native.Point { X = (bounds.Left + bounds.Right) / 2, Y = bounds.Bottom - 1 },
                new Native.Point { X = bounds.Left, Y = (bounds.Top + bounds.Bottom) / 2 },
                new Native.Point { X = bounds.Right - 1, Y = (bounds.Top + bounds.Bottom) / 2 }
            };
            string[] actual = Array.Empty<string>();
            for (int attempt = 0; attempt < 20; attempt++)
            {
                await Task.Delay(100); ScreenPixels.DwmFlush();
                using var pixels = ScreenPixels.Capture(bounds);
                actual = points.Select(point => ScreenPixels.Hex(pixels.Sample(point))).ToArray();
                if (actual.All(color => color == expected))
                {
                    File.AppendAllText(report, "PASS: " + label + "\n"); return;
                }
            }
            throw new Exception("FAIL: " + label + "; expected " + expected + ", top/bottom/left/right: " + string.Join(", ", actual)
                + $"; window {bounds.Width}x{bounds.Height}, view {app.Clock.View.ActualWidth}x{app.Clock.View.ActualHeight}, style {Native.GetWindowLong(Native.Handle(app.Clock), -16):X8}");
        }
    }
}
