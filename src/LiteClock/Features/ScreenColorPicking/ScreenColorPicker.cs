using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;

namespace LiteClock;

internal sealed class ScreenColorPicker
{
    readonly Window owner;
    readonly List<PickerWindow> windows = new();
    readonly List<ScreenPixels> captures = new();
    readonly TaskCompletionSource<string> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    bool finished, ready, ownerClosed;

    ScreenColorPicker(Window owner) { this.owner = owner; }
    public static Task<string> PickAsync(Window owner, CancellationToken cancellationToken = default)
        => new ScreenColorPicker(owner).RunAsync(cancellationToken);

    async Task<string> RunAsync(CancellationToken token)
    {
        void OwnerClosed(object sender, WindowEventArgs args) { ownerClosed = true; Finish(null); }
        owner.Closed += OwnerClosed;
        using var cancellation = token.Register(() => owner.DispatcherQueue.TryEnqueue(() => Finish(null)));
        try
        {
            owner.AppWindow.Hide();
            // Allow the picker flyout and owner to leave the composed desktop.
            await Task.Delay(150, token);
            if (finished) return null;
            ScreenPixels.DwmFlush();
            // Capture every monitor before showing any overlay, so neither the
            // magnifier nor a neighboring overlay can contaminate sampled pixels.
            foreach (var monitor in Native.Monitors()) captures.Add(ScreenPixels.Capture(monitor.Bounds));
            foreach (var capture in captures) windows.Add(new PickerWindow(this, capture));
            foreach (var window in windows) window.AppWindow.Show(false);
            Native.GetCursorPos(out var cursor);
            var active = windows.FirstOrDefault(w => w.Pixels.Contains(cursor)) ?? windows.First();
            // Expose one focused picker in the switcher/accessibility window list.
            // Switching to another application cancels the modal picking session.
            active.AppWindow.IsShownInSwitchers = true;
            var activeHandle = Native.Handle(active);
            Native.SetWindowLong(activeHandle, -20, (Native.GetWindowLong(activeHandle, -20) | 0x40000) & ~0x80);
            active.Activate(); Native.SetForegroundWindow(activeHandle);
            ready = true;
            active.UpdatePreview(cursor);
            return await completion.Task;
        }
        catch (OperationCanceledException) { return null; }
        finally
        {
            Finish(null);
            owner.Closed -= OwnerClosed;
            if (!ownerClosed) { owner.Activate(); Native.SetForegroundWindow(Native.Handle(owner)); }
        }
    }
    void Accept(Native.Point point)
    {
        var capture = captures.FirstOrDefault(c => c.Contains(point));
        if (capture != null) Finish(ScreenPixels.Hex(capture.Sample(point)));
    }
    void Finish(string color)
    {
        if (finished) return;
        finished = true; ready = false;
        foreach (var window in windows) window.Close();
        windows.Clear();
        foreach (var capture in captures) capture.Dispose();
        captures.Clear();
        completion.TrySetResult(color);
    }
    void CheckActivation()
    {
        if (!ready) return;
        owner.DispatcherQueue.TryEnqueue(() =>
        {
            if (ready && windows.All(w => Native.Handle(w) != Native.GetForegroundWindow())) Finish(null);
        });
    }

    sealed class PickerSurface : Grid
    {
        public PickerSurface()
        {
            Background = new SolidColorBrush(Colors.Transparent);
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);
        }
    }
    sealed class PickerWindow : Window
    {
        public ScreenPixels Pixels { get; }
        readonly ScreenColorPicker session;
        readonly PickerSurface root = new();
        readonly Border card, swatch;
        readonly TextBlock hex;
        readonly WriteableBitmap zoom = new(88, 88);
        readonly Native.SubclassProc hook;
        readonly IntPtr hwnd;

        public PickerWindow(ScreenColorPicker session, ScreenPixels pixels)
        {
            this.session = session; Pixels = pixels;
            Title = AppDetails.WindowTitle("屏幕取色"); Content = root;
            ((OverlappedPresenter)AppWindow.Presenter).IsAlwaysOnTop = true;
            Native.ToolWindow(this);
            hwnd = Native.Handle(this);
            hook = (IntPtr window, uint message, IntPtr w, IntPtr l, UIntPtr id, IntPtr data) =>
            {
                // A display-mode change invalidates the captured pixel geometry.
                if (message == 0x7E) DispatcherQueue.TryEnqueue(() => session.Finish(null));
                return Native.DefSubclassProc(window, message, w, l);
            };
            Native.SetWindowSubclass(hwnd, hook, (UIntPtr)3, IntPtr.Zero);
            var bounds = pixels.Bounds;
            Native.SetWindowPos(hwnd, new IntPtr(-1), bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x10);

            var bitmap = new WriteableBitmap(pixels.Width, pixels.Height);
            using (var stream = bitmap.PixelBuffer.AsStream()) stream.Write(pixels.Bytes, 0, pixels.Bytes.Length);
            bitmap.Invalidate();
            root.Children.Add(new Image { Source = bitmap, Stretch = Stretch.Fill, IsHitTestVisible = false });
            // A focused native control routes Escape through the XAML tree even
            // though the visible picker consists entirely of images and labels.
            var keyboardTarget = new Button { Width = 1, Height = 1, Opacity = 0, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            AutomationProperties.SetName(keyboardTarget, "按 Escape 取消屏幕取色");
            root.Children.Add(keyboardTarget);
            root.Loaded += (_, _) =>
            {
                keyboardTarget.Focus(FocusState.Programmatic);
                Native.GetCursorPos(out var point); UpdatePreview(point);
            };
            var canvas = new Canvas { IsHitTestVisible = false }; root.Children.Add(canvas);
            var preview = new Grid { Width = 88, Height = 88 };
            preview.Children.Add(new Image { Source = zoom });
            preview.Children.Add(new Border { Width = 10, Height = 10, BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Colors.White), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            preview.Children.Add(new Border { Width = 8, Height = 8, BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Colors.Black), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            swatch = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(4), BorderBrush = new SolidColorBrush(Colors.Gray), BorderThickness = new Thickness(1) };
            hex = new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 18, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Colors.White) };
            var colorRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            colorRow.Children.Add(swatch); colorRow.Children.Add(hex);
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(preview); panel.Children.Add(colorRow);
            panel.Children.Add(new TextBlock { Text = "左键取色 · 右键 / Esc 取消", FontSize = 12, Foreground = new SolidColorBrush(Colors.White) });
            card = new Border { Child = panel, Padding = new Thickness(12), CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(245, 32, 32, 32)), BorderBrush = new SolidColorBrush(Colors.Gray), BorderThickness = new Thickness(1), Visibility = Visibility.Collapsed };
            canvas.Children.Add(card);
            AutomationProperties.SetName(root, "屏幕取色，左键确认，右键或 Escape 取消");
            root.PointerMoved += (_, e) => { Native.GetCursorPos(out var point); UpdatePreview(point); e.Handled = true; };
            root.PointerExited += (_, _) => card.Visibility = Visibility.Collapsed;
            root.PointerPressed += (_, e) => { root.CapturePointer(e.Pointer); e.Handled = true; };
            root.PointerReleased += (_, e) =>
            {
                var kind = e.GetCurrentPoint(root).Properties.PointerUpdateKind;
                root.ReleasePointerCapture(e.Pointer); e.Handled = true;
                if (kind == Microsoft.UI.Input.PointerUpdateKind.LeftButtonReleased)
                {
                    Native.GetCursorPos(out var point); session.Accept(point);
                }
                else if (kind == Microsoft.UI.Input.PointerUpdateKind.RightButtonReleased) session.Finish(null);
            };
            // PreviewKeyDown catches Escape regardless of focus in the XAML tree.
            root.PreviewKeyDown += (_, e) => { if (e.Key == VirtualKey.Escape) { e.Handled = true; session.Finish(null); } };
            Activated += (_, e) => { if (e.WindowActivationState == WindowActivationState.Deactivated) session.CheckActivation(); };
            AppWindow.Closing += (_, e) => { if (!session.finished) { e.Cancel = true; session.Finish(null); } };
            Closed += (_, _) =>
            {
                Native.RemoveWindowSubclass(hwnd, hook, (UIntPtr)3);
                root.Children.Clear();
            };
        }
        public void UpdatePreview(Native.Point point)
        {
            if (session.finished || !Pixels.Contains(point) || root.ActualWidth <= 0 || root.ActualHeight <= 0) return;
            var color = Pixels.Sample(point); hex.Text = ScreenPixels.Hex(color); swatch.Background = new SolidColorBrush(color);
            var magnified = Pixels.Magnify(point);
            using (var stream = zoom.PixelBuffer.AsStream()) stream.Write(magnified, 0, magnified.Length);
            zoom.Invalidate();
            card.Visibility = Visibility.Visible;
            double x = (point.X - Pixels.Bounds.Left) * root.ActualWidth / Pixels.Width;
            double y = (point.Y - Pixels.Bounds.Top) * root.ActualHeight / Pixels.Height;
            double width = Math.Max(220, card.ActualWidth), height = Math.Max(184, card.ActualHeight);
            Canvas.SetLeft(card, Math.Clamp(x + width + 24 < root.ActualWidth ? x + 24 : x - width - 24, 0, Math.Max(0, root.ActualWidth - width)));
            Canvas.SetTop(card, Math.Clamp(y + height + 24 < root.ActualHeight ? y + 24 : y - height - 24, 0, Math.Max(0, root.ActualHeight - height)));
        }
    }
}
