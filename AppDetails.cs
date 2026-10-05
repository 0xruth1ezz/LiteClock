using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LiteClock;

internal static class AppDetails
{
    public const string Name = "轻时钟 LiteClock";
    public const string Description = "轻量、可自定义的 Windows 桌面时钟。";
    // Read the project version from the built assembly; omit source-control
    // metadata from the short version shown in window titles.
    public static string Version { get; } =
        typeof(AppDetails).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(AppDetails).Assembly.GetName().Version?.ToString(3) ?? "未知";
    public static string DisplayTitle => $"{Name} v{Version}";
    public static string WindowTitle(string page) => $"{DisplayTitle} · {page}";
    public static IReadOnlyList<(string Label, string Value)> EnvironmentDetails => new[]
    {
        ("应用版本", Version),
        ("界面框架", "WinUI 3"),
        ("运行环境", $"{RuntimeInformation.FrameworkDescription} · {RuntimeInformation.ProcessArchitecture}"),
        ("操作系统", RuntimeInformation.OSDescription)
    };
    public static IReadOnlyList<(string Label, string Value)> FileDetails => new[]
    {
        ("程序位置", ClockApp.ExePath),
        ("配置文件", Store.ConfigPath)
    };
    public static string CopyText() => Name + Environment.NewLine
        + string.Join(Environment.NewLine, EnvironmentDetails.Concat(FileDetails).Select(item => $"{item.Label}：{item.Value}"));
}
