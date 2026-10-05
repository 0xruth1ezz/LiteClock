using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Composition;
using ICompositionSupportsSystemBackdrop = Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop;

namespace LiteClock;

// An explicit transparent composition backdrop lets WinUI content alpha reveal
// the desktop, including the clock's rounded corners and independent text alpha.
public sealed class TransparentBackdrop : SystemBackdrop
{
    [StructLayout(LayoutKind.Sequential)] struct QueueOptions { public int Size, ThreadType, ApartmentType; }
    [DllImport("CoreMessaging.dll")] static extern int CreateDispatcherQueueController(QueueOptions options, out IntPtr controller);
    static IntPtr queueController;
    Compositor compositor;
    CompositionColorBrush brush;
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot root)
    {
        if (Windows.System.DispatcherQueue.GetForCurrentThread() == null && queueController == IntPtr.Zero)
            Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(new QueueOptions { Size = Marshal.SizeOf<QueueOptions>(), ThreadType = 2, ApartmentType = 2 }, out queueController));
        compositor = new Compositor();
        brush = compositor.CreateColorBrush(Colors.Transparent);
        target.SystemBackdrop = brush;
    }
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        target.SystemBackdrop = null; brush?.Dispose(); brush = null; compositor?.Dispose(); compositor = null;
    }
}
