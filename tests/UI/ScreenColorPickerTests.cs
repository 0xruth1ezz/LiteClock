using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LiteClock;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

static class ScreenColorPickerTests
{
    static void Check(bool condition, string label, string report)
    {
        if (!condition) throw new Exception("FAIL: " + label);
        File.AppendAllText(report, "PASS: " + label + "\n");
    }
    public static async Task Run(ClockApp app, string report)
    {
        var bounds = new Native.Rect { Left = -200, Top = -10, Right = -198, Bottom = -8 };
        var data = new byte[] { 0x56, 0x34, 0x12, 255, 0xFF, 0, 0, 255, 0, 0xFF, 0, 255, 0, 0, 0xFF, 255 };
        using (var pixels = new ScreenPixels(bounds, data))
        {
            Check(ScreenPixels.Hex(pixels.Sample(new Native.Point { X = -200, Y = -10 })) == "#123456", "screen BGRA converts to RGB with negative monitor coordinates", report);
            Check(ScreenPixels.Hex(pixels.Sample(new Native.Point { X = -199, Y = -9 })) == "#FF0000", "screen bitmap rows are top-down and bottom-right pixel is reachable", report);
            Check(!pixels.Contains(new Native.Point { X = -198, Y = -9 }) && !pixels.Contains(new Native.Point { X = -199, Y = -8 }), "screen right and bottom edges are exclusive", report);
            var zoom = pixels.Magnify(new Native.Point { X = -200, Y = -10 });
            int center = (44 * 88 + 44) * 4;
            Check(zoom[center] == 0x56 && zoom[center + 1] == 0x34 && zoom[center + 2] == 0x12 && zoom[0] == 0x56,
                "magnifier center matches sampled pixel and clamps at monitor edges", report);
        }
        Check(Array.TrueForAll(data, b => b == 0), "capture buffers are cleared after use", report);

        var surface = new Grid { Background = new SolidColorBrush(Settings.ParseColor("#123456")) };
        var loaded = new TaskCompletionSource();
        surface.Loaded += (_, _) => loaded.TrySetResult();
        var host = new Window { Title = "轻时钟 · 取色测试", Content = surface };
        ((OverlappedPresenter)host.AppWindow.Presenter).IsAlwaysOnTop = true;
        Native.Center(host, 300, 200); host.Activate(); Native.SetForegroundWindow(Native.Handle(host));
        try
        {
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Native.GetWindowRect(Native.Handle(host), out var actualBounds);
            var sample = new Native.Point { X = (actualBounds.Left + actualBounds.Right) / 2, Y = (actualBounds.Top + actualBounds.Bottom) / 2 };
            string sampled = null;
            // XAML Loaded precedes the desktop compositor's first completed frame.
            // Wait for that frame instead of assuming a fixed startup duration.
            for (int attempt = 0; attempt < 20; attempt++)
            {
                await Task.Delay(100); ScreenPixels.DwmFlush();
                using var capture = ScreenPixels.Capture(actualBounds);
                sampled = ScreenPixels.Hex(capture.Sample(sample));
                if (sampled == "#123456") break;
            }
            Check(sampled == "#123456", "native desktop capture samples a known WinUI surface correctly (actual " + sampled + ")", report);
            using var cancel = new CancellationTokenSource();
            var task = ScreenColorPicker.PickAsync(app.Editor, cancel.Token);
            await Task.Delay(400);
            Check(!app.Editor.AppWindow.IsVisible, "screen picking hides settings while sampling", report);
            cancel.Cancel();
            Check(await task == null && app.Editor.AppWindow.IsVisible, "cancel closes overlays and restores settings without a color", report);
        }
        finally { host.Close(); }
    }
}
