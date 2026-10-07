using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LiteClock;

// A non-activating clock cannot rely on a focus change to dismiss its menu:
// clicking the already-foreground app does not produce one. Observe clicks only
// while the flyout is open, without consuming or redirecting any input.
internal sealed class MenuDismissal : IDisposable
{
    delegate IntPtr MouseProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int hookId, MouseProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Native.Point point);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);

    readonly Window owner;
    readonly MenuFlyout menu;
    readonly MouseProc callback;
    IntPtr hook;
    int generation;
    bool disposed;
    internal bool IsListening => hook != IntPtr.Zero;

    public MenuDismissal(Window owner, MenuFlyout menu)
    {
        this.owner = owner; this.menu = menu; callback = OnMouse;
        menu.Opened += Opened; menu.Closed += Closed; owner.Closed += OwnerClosed;
    }
    void Opened(object sender, object args)
    {
        if (disposed || IsListening) return;
        generation++;
        hook = SetWindowsHookEx(14, callback, GetModuleHandle(null), 0); // WH_MOUSE_LL
        if (hook == IntPtr.Zero) Store.Log(new Win32Exception(Marshal.GetLastWin32Error(), "Could not observe outside menu clicks."));
    }
    IntPtr OnMouse(int code, IntPtr message, IntPtr data)
    {
        long kind = message.ToInt64();
        if (code >= 0 && data != IntPtr.Zero && kind is 0x201 or 0x204 or 0x207 or 0x20B)
            PointerDown(Marshal.PtrToStructure<Native.Point>(data)); // MSLLHOOKSTRUCT starts with POINT.
        return CallNextHookEx(IntPtr.Zero, code, message, data);
    }
    internal void PointerDown(Native.Point point)
    {
        if (!IsListening) return;
        GetWindowThreadProcessId(WindowFromPoint(point), out uint processId);
        // The clock, settings, popup HWNDs and nested submenus all belong to us.
        // Testing the actual window also handles overlap, DPI and other monitors.
        if (processId == (uint)Environment.ProcessId) return;
        int current = generation;
        owner.DispatcherQueue.TryEnqueue(() =>
        {
            if (IsListening && current == generation) menu.Hide();
        });
    }
    void Closed(object sender, object args) => StopListening();
    void OwnerClosed(object sender, WindowEventArgs args) => Dispose();
    void StopListening()
    {
        generation++;
        if (hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(hook); hook = IntPtr.Zero;
        GC.KeepAlive(callback);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; StopListening();
        menu.Opened -= Opened; menu.Closed -= Closed; owner.Closed -= OwnerClosed;
    }
}
