using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LiteClock;

public sealed class TrayIcon : IDisposable
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NotifyData
    {
        public uint Size; public IntPtr Window; public uint Id, Flags, Callback;
        public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
        public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIcon(uint message, ref NotifyData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadImage(IntPtr module, string name, uint type, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    public const uint CallbackMessage = 0x8001;
    public static readonly uint TaskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
    NotifyData data;
    public TrayIcon(IntPtr hwnd)
    {
        data = new NotifyData
        {
            Size = (uint)Marshal.SizeOf<NotifyData>(), Window = hwnd, Id = 1,
            Flags = 1 | 2 | 4, Callback = CallbackMessage,
            Icon = LoadImage(IntPtr.Zero, Path.Combine(AppContext.BaseDirectory, "LiteClock.ico"), 1, 32, 32, 0x10),
            Tip = "轻时钟 · 双击打开设置，右键打开菜单"
        };
        if (data.Icon == IntPtr.Zero) throw new InvalidOperationException("无法加载托盘图标。");
        Restore();
    }
    public void Restore()
    {
        if (!Shell_NotifyIcon(0, ref data)) Store.Log(new InvalidOperationException("托盘图标注册失败。可再次运行程序打开设置。"));
    }
    public void Notify(string message)
    {
        data.Flags |= 0x10; data.Info = message; data.Title = "轻时钟"; data.InfoFlags = 2;
        Shell_NotifyIcon(1, ref data); data.Flags &= ~0x10u;
    }
    public void Dispose()
    {
        Shell_NotifyIcon(2, ref data);
        if (data.Icon != IntPtr.Zero) { DestroyIcon(data.Icon); data.Icon = IntPtr.Zero; }
    }
}
