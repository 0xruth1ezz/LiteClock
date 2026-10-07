using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.UI;

namespace LiteClock;

public sealed class Settings
{
    public double Width { get; set; }
    public double Height { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
    public string FontFamily { get; set; }
    public double FontPoints { get; set; }
    public bool Bold { get; set; }
    public string Foreground { get; set; }
    public string Background { get; set; }
    public double BackgroundOpacity { get; set; }
    public double LineHeight { get; set; }
    public string Format { get; set; }
    public string Culture { get; set; }
    public string Monitor { get; set; }
    public bool HideFullscreen { get; set; }
    public bool LockPosition { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public string Alignment { get; set; }
    public string Vertical { get; set; }
    public double PaddingLeft { get; set; }
    public double PaddingRight { get; set; }
    public double PaddingTop { get; set; }
    public double PaddingBottom { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double LineGap { get; set; }
    public double Radius { get; set; }
    public double BorderWidth { get; set; }
    public string BorderColor { get; set; }
    public double BorderOpacity { get; set; }
    public double TextOpacity { get; set; }
    public double WindowOpacity { get; set; }
    public bool HoverEnabled { get; set; }
    public string HoverBackground { get; set; }
    public string HoverForeground { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool ClickThrough { get; set; }
    public string ClickAction { get; set; }
    public string DoubleClickAction { get; set; }
    public string MiddleClickAction { get; set; }
    public int RefreshMilliseconds { get; set; }
    public string TimeZone { get; set; }
    public double TimeOffsetMinutes { get; set; }
    public List<LineStyle> Lines { get; set; }
    public Settings()
    {
        Width = 68; Height = 46; Right = 0; Bottom = 56;
        FontFamily = "Microsoft YaHei UI"; FontPoints = 8; Bold = true;
        Foreground = "#000000"; Background = "#D1EAF2"; BackgroundOpacity = 100;
        LineHeight = 14; Format = "%H:%M:%S\n%m-%d\n%a";
        Culture = "zh-SG"; Monitor = ""; HideFullscreen = true;
        Alignment = "Center"; Vertical = "Center"; PaddingLeft = PaddingRight = 2;
        BorderColor = "#7BA5B4"; BorderOpacity = TextOpacity = WindowOpacity = 100;
        HoverBackground = "#BFE0EA"; HoverForeground = "#000000";
        AlwaysOnTop = true;
        ClickAction = "None"; DoubleClickAction = "Settings"; MiddleClickAction = "Copy";
        RefreshMilliseconds = 250; TimeZone = "Local";
        Lines = Enumerable.Range(0, 8).Select(i => new LineStyle()).ToList();
    }
    public Settings Copy()
    {
        var result = (Settings)MemberwiseClone(); result.Lines = Lines.Select(l => l.Copy()).ToList(); return result;
    }
    public DateTime DisplayTime(DateTime utc)
    {
        DateTime time = TimeZone == "Local" ? utc.ToLocalTime() : TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(TimeZone));
        return time.AddMinutes(TimeOffsetMinutes);
    }
    public static void OneOf(string value, string[] allowed, string label)
    {
        if (!allowed.Contains(value)) throw new ArgumentException(label + "选项无效。");
    }
    public static void Range(double value, double min, double max, string label)
    {
        if (Double.IsNaN(value) || Double.IsInfinity(value) || value < min || value > max)
            throw new ArgumentException(label + "应在 " + min + "–" + max + " 之间。");
    }
    public void Validate()
    {
        Range(Width, 24, 1200, "宽度"); Range(Height, 24, 600, "高度");
        Range(Right, 0, 20000, "右边距"); Range(Bottom, 0, 20000, "下边距");
        Range(FontPoints, 5, 72, "字号"); Range(LineHeight, 8, 120, "行高");
        Range(BackgroundOpacity, 0, 100, "背景不透明度");
        Range(TextOpacity, 0, 100, "文字不透明度"); Range(WindowOpacity, 5, 100, "整体不透明度");
        Range(PaddingLeft, 0, 300, "左内边距"); Range(PaddingRight, 0, 300, "右内边距");
        Range(PaddingTop, 0, 300, "上内边距"); Range(PaddingBottom, 0, 300, "下内边距");
        Range(OffsetX, -500, 500, "文字水平偏移"); Range(OffsetY, -500, 500, "文字垂直偏移");
        Range(LineGap, 0, 100, "行间距"); Range(Radius, 0, 300, "圆角"); Range(BorderWidth, 0, 30, "边框宽度");
        Range(BorderOpacity, 0, 100, "边框不透明度"); Range(RefreshMilliseconds, 100, 60000, "刷新间隔");
        Range(TimeOffsetMinutes, -10080, 10080, "时间偏移");
        OneOf(Alignment, new[] { "Left", "Center", "Right" }, "水平对齐");
        OneOf(Vertical, new[] { "Top", "Center", "Bottom" }, "垂直对齐");
        foreach (string action in new[] { ClickAction, DoubleClickAction, MiddleClickAction })
            OneOf(action, new[] { "None", "Settings", "Calendar", "Copy" }, "点击行为");
        ParseColor(BorderColor); ParseColor(HoverBackground); ParseColor(HoverForeground);
        if (TimeZone != "Local") TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        if (Lines == null || Lines.Count != 8 || Lines.Any(l => l == null)) throw new ArgumentException("单行设置需要 8 个有效项目。");
        foreach (LineStyle line in Lines) line.Validate();
        if (String.IsNullOrWhiteSpace(FontFamily) || FontFamily.Length > 100) throw new ArgumentException("请填写字体名称。");
        ParseColor(Foreground); ParseColor(Background);
        CultureInfo.GetCultureInfo(Culture);
        if (String.IsNullOrWhiteSpace(Format) || Format.Length > 512 || Format.Split('\n').Length > 8)
            throw new ArgumentException("显示格式应为 1–8 行，最多 512 个字符。");
        if (ClockFormat.Render(Format, DateTime.Now, CultureInfo.GetCultureInfo(Culture)).Split('\n').Length > 8)
            throw new ArgumentException("最多显示 8 行，包括 %n 换行符。");
    }
    public static Color ParseColor(string text)
    {
        if (text == null || !System.Text.RegularExpressions.Regex.IsMatch(text, "^#[0-9a-fA-F]{6}$"))
            throw new ArgumentException("颜色请使用 #RRGGBB，例如 #D1EAF2。");
        return Color.FromArgb(255, Convert.ToByte(text.Substring(1, 2), 16), Convert.ToByte(text.Substring(3, 2), 16), Convert.ToByte(text.Substring(5, 2), 16));
    }
}
