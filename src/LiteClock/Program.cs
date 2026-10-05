using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace LiteClock;

static class Program
{
    static string instanceSuffix = "";
    public static string Channel(string action) => "Local\\LiteClock." + action + ".v1" + instanceSuffix;
    public static string ArgumentAfter(string[] args, string name)
    {
        int index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
    [STAThread]
    static int Main(string[] args)
    {
        string settingsDirectory = ArgumentAfter(args, "--settings-dir");
        if (settingsDirectory != null) { Store.DirectoryPath = Path.GetFullPath(settingsDirectory); Directory.CreateDirectory(Store.DirectoryPath); }
        if (args.Contains("--self-test")) return SelfTests.Run(new[] { "--self-test", ArgumentAfter(args, "--self-test") ?? Path.Combine(Store.DirectoryPath, "self-test.txt") });
        bool testing = args.Contains("--ui-test");
        if (settingsDirectory != null) instanceSuffix = "." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Store.DirectoryPath.ToUpperInvariant()))).Substring(0, 16);
        else if (testing) instanceSuffix = ".UiTest";
        using var mutex = new Mutex(true, Channel("SingleInstance"), out bool created);
        if (args.Contains("--exit")) { Signal(Program.Channel("Exit")); return 0; }
        if (!created) { Signal(Program.Channel("ShowSettings")); return 0; }
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(callback =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                _ = new ClockApp(args);
            });
            return Environment.ExitCode;
        }
        catch (Exception ex) { Store.Log(ex); Native.MessageBox(IntPtr.Zero, ex.Message, "轻时钟启动失败", 0x10); return 1; }
        finally { mutex.ReleaseMutex(); }
    }
    static void Signal(string name)
    {
        try { using var handle = EventWaitHandle.OpenExisting(name); handle.Set(); }
        catch (WaitHandleCannotBeOpenedException) { }
    }
}
