using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.UI;

namespace LiteClock;

// Desktop pixels are captured once, in physical coordinates. No image is saved.
// Both the frozen display and the selected color use this same BGRA buffer.
internal sealed class ScreenPixels : IDisposable
{
    public Native.Rect Bounds { get; }
    public byte[] Bytes { get; }
    public int Width => Bounds.Width;
    public int Height => Bounds.Height;

    internal ScreenPixels(Native.Rect bounds, byte[] bytes)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || bytes.Length != checked(bounds.Width * bounds.Height * 4))
            throw new ArgumentException("屏幕图像尺寸无效。");
        Bounds = bounds; Bytes = bytes;
    }
    public bool Contains(Native.Point point) => point.X >= Bounds.Left && point.X < Bounds.Right
        && point.Y >= Bounds.Top && point.Y < Bounds.Bottom;
    public Color Sample(Native.Point point)
    {
        if (!Contains(point)) throw new ArgumentOutOfRangeException(nameof(point));
        int index = ((point.Y - Bounds.Top) * Width + point.X - Bounds.Left) * 4;
        return Color.FromArgb(255, Bytes[index + 2], Bytes[index + 1], Bytes[index]);
    }
    public static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    // Expand each source pixel explicitly so the WinUI magnifier stays crisp.
    public byte[] Magnify(Native.Point point, int diameter = 11, int zoom = 8)
    {
        if (!Contains(point)) throw new ArgumentOutOfRangeException(nameof(point));
        if (diameter < 1 || diameter % 2 == 0 || zoom < 1) throw new ArgumentOutOfRangeException(nameof(diameter));
        int side = checked(diameter * zoom);
        var result = new byte[checked(side * side * 4)];
        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            int sx = Math.Clamp(point.X - Bounds.Left + x / zoom - diameter / 2, 0, Width - 1);
            int sy = Math.Clamp(point.Y - Bounds.Top + y / zoom - diameter / 2, 0, Height - 1);
            Buffer.BlockCopy(Bytes, (sy * Width + sx) * 4, result, (y * side + x) * 4, 4);
        }
        return result;
    }
    public void Dispose() => Array.Clear(Bytes);

    [StructLayout(LayoutKind.Sequential)]
    struct BitmapInfo
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount;
        public uint Compression, SizeImage; public int XPelsPerMeter, YPelsPerMeter;
        public uint ClrUsed, ClrImportant, Colors;
    }
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
    [DllImport("gdi32.dll", SetLastError = true)] static extern bool BitBlt(IntPtr dest, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool GdiFlush();
    [DllImport("dwmapi.dll")] internal static extern int DwmFlush();

    public static ScreenPixels Capture(Native.Rect bounds)
    {
        int length = checked(bounds.Width * bounds.Height * 4);
        if (bounds.Width <= 0 || bounds.Height <= 0) throw new ArgumentException("显示器尺寸无效。");
        IntPtr screen = IntPtr.Zero, memory = IntPtr.Zero, bitmap = IntPtr.Zero, previous = IntPtr.Zero;
        try
        {
            screen = GetDC(IntPtr.Zero);
            if (screen == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法读取屏幕。");
            memory = CreateCompatibleDC(screen);
            if (memory == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            // A negative height creates a top-down DIB, matching WinUI buffers.
            var info = new BitmapInfo { Size = 40, Width = bounds.Width, Height = -bounds.Height, Planes = 1, BitCount = 32 };
            bitmap = CreateDIBSection(screen, ref info, 0, out var bits, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero || bits == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            previous = SelectObject(memory, bitmap);
            if (previous == IntPtr.Zero || previous == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!BitBlt(memory, 0, 0, bounds.Width, bounds.Height, screen, bounds.Left, bounds.Top, 0x40CC0020))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法捕获屏幕画面。");
            GdiFlush();
            var bytes = new byte[length]; Marshal.Copy(bits, bytes, 0, length);
            // Desktop GDI does not define alpha; the captured image is opaque.
            for (int i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
            return new ScreenPixels(bounds, bytes);
        }
        finally
        {
            if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(memory, previous);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (memory != IntPtr.Zero) DeleteDC(memory);
            if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
        }
    }
}
