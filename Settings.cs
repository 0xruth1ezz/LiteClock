using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Windows.UI;

namespace LiteClock
{
    public sealed class LineStyle
    {
        public bool Enabled { get; set; }
        public bool Custom { get; set; }
        public string FontFamily { get; set; }
        public double FontPoints { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public string Color { get; set; }
        public string Alignment { get; set; }
        public double Height { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public LineStyle()
        {
            Enabled = true; FontFamily = "Microsoft YaHei UI"; FontPoints = 8; Bold = true;
            Color = "#000000"; Alignment = "Center"; Height = 14;
        }
        public LineStyle Copy() { return (LineStyle)MemberwiseClone(); }
        public void Validate()
        {
            Settings.Range(FontPoints, 5, 72, "单行字号"); Settings.Range(Height, 8, 160, "单行高度");
            Settings.Range(OffsetX, -500, 500, "单行水平偏移"); Settings.Range(OffsetY, -500, 500, "单行垂直偏移");
            Settings.ParseColor(Color); Settings.OneOf(Alignment, new[] { "Left", "Center", "Right" }, "单行对齐");
            if (String.IsNullOrWhiteSpace(FontFamily) || FontFamily.Length > 100) throw new ArgumentException("请填写单行字体。");
        }
    }
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
        public bool TooltipEnabled { get; set; }
        public string Tooltip { get; set; }
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
            AlwaysOnTop = TooltipEnabled = true;
            Tooltip = "拖动左边缘调整宽度\nShift + 拖动：移动位置\n双击或右键：设置";
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
            if (Tooltip == null || Tooltip.Length > 1000) throw new ArgumentException("提示文字最多 1000 字。");
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

    public static class ClockFormat
    {
        public static string Render(string format, DateTime time, CultureInfo culture)
        {
            var text = new StringBuilder();
            for (int i = 0; i < format.Length; i++)
            {
                char c = format[i];
                if (c != '%') { if (c != '\r') text.Append(c); continue; }
                if (++i >= format.Length) throw new ArgumentException("单独的 % 无效；显示百分号请写 %% 。");
                switch (format[i])
                {
                    case '%': text.Append('%'); break;
                    case 'H': text.Append(time.ToString("HH", culture)); break;
                    case 'M': text.Append(time.ToString("mm", culture)); break;
                    case 'S': text.Append(time.ToString("ss", culture)); break;
                    case 'I': text.Append(time.ToString("hh", culture)); break;
                    case 'p': text.Append(time.ToString("tt", culture)); break;
                    case 'Y': text.Append(time.ToString("yyyy", culture)); break;
                    case 'y': text.Append(time.ToString("yy", culture)); break;
                    case 'm': text.Append(time.ToString("MM", culture)); break;
                    case 'd': text.Append(time.ToString("dd", culture)); break;
                    case 'a': text.Append(time.ToString("ddd", culture)); break;
                    case 'A': text.Append(time.ToString("dddd", culture)); break;
                    case 'b': text.Append(time.ToString("MMM", culture)); break;
                    case 'B': text.Append(time.ToString("MMMM", culture)); break;
                    case 'j': text.Append(time.DayOfYear.ToString("000", culture)); break;
                    case 'w': text.Append((int)time.DayOfWeek); break;
                    case 'F': text.Append(time.ToString("yyyy-MM-dd", culture)); break;
                    case 'T': text.Append(time.ToString("HH:mm:ss", culture)); break;
                    case 'n': text.Append('\n'); break;
                    default: throw new ArgumentException("不支持的格式：%" + format[i]);
                }
            }
            return text.ToString();
        }
    }

    public static class Store
    {
        public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
        public static string Serialize(Settings settings) => JsonSerializer.Serialize(settings, JsonOptions);
        public static string DirectoryPath = AppContext.BaseDirectory;
        public static string ConfigPath { get { return Path.Combine(DirectoryPath, "settings.json"); } }
        public static string RecoveryMessage;
        public static Settings Load()
        {
            try { return Read(ConfigPath); }
            catch (FileNotFoundException) { return new Settings(); }
            catch (Exception ex)
            {
                Log(ex);
                try
                {
                    Settings backup = Read(ConfigPath + ".bak");
                    RecoveryMessage = "配置文件损坏，已读取上一次备份。原文件保留在程序目录。";
                    return backup;
                }
                catch { RecoveryMessage = "配置无法读取，已使用默认显示。原文件保留在程序目录。"; return new Settings(); }
            }
        }
        public static Settings Read(string path)
        {
            var result = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path, Encoding.UTF8), JsonOptions);
            if (result == null) throw new InvalidDataException("配置为空。");
            result.Validate(); return result;
        }
        public static void Save(Settings settings)
        {
            settings.Validate();
            string temporary = ConfigPath + ".tmp";
            File.WriteAllText(temporary, Serialize(settings), new UTF8Encoding(false));
            if (File.Exists(ConfigPath)) File.Replace(temporary, ConfigPath, ConfigPath + ".bak");
            else File.Move(temporary, ConfigPath);
        }
        public static void Log(Exception error)
        {
            try
            {
                string path = Path.Combine(DirectoryPath, "LiteClock.log");
                if (File.Exists(path) && new FileInfo(path).Length > 256000)
                    File.WriteAllText(path, "Log rotated\r\n");
                File.AppendAllText(path, DateTime.Now.ToString("o") + " " + error + Environment.NewLine);
            }
            catch { }
        }
    }

}
