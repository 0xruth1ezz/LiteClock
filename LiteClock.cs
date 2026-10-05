// LiteClock - an independent WPF clock. No ElevenClock code or runtime is used.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

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
            return (Color)ColorConverter.ConvertFromString(text);
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
        public static string DirectoryPath = AppDomain.CurrentDomain.BaseDirectory;
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
            var result = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(path, Encoding.UTF8));
            if (result == null) throw new InvalidDataException("配置为空。");
            result.Validate(); return result;
        }
        public static void Save(Settings settings)
        {
            settings.Validate();
            string temporary = ConfigPath + ".tmp";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(settings), new UTF8Encoding(false));
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

    public static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] public static extern int GetClassName(IntPtr window, StringBuilder name, int count);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr window, int index, int value);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
        public static bool IsFullscreen(IntPtr own, Drawing.Rectangle screen)
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero || window == own) return false;
            var name = new StringBuilder(256); GetClassName(window, name, name.Capacity);
            if (new[] { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" }.Contains(name.ToString())) return false;
            Rect rect;
            return GetWindowRect(window, out rect) && rect.Left <= screen.Left && rect.Top <= screen.Top &&
                rect.Right >= screen.Right && rect.Bottom >= screen.Bottom;
        }
    }

    public sealed class ClockApp : Application
    {
        public static string ExePath { get { return System.Reflection.Assembly.GetExecutingAssembly().Location; } }
        static ImageSource windowIcon;
        public static ImageSource WindowIcon
        {
            get
            {
                if (windowIcon == null)
                {
                    using (Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("LiteClock.AppIcon"))
                    {
                        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(stream,
                            System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                        windowIcon = decoder.Frames.OrderByDescending(frame => frame.PixelWidth).First();
                        windowIcon.Freeze();
                    }
                }
                return windowIcon;
            }
        }
        public ClockWindow Clock;
        public SettingsWindow Editor;
        Forms.NotifyIcon tray;
        EventWaitHandle settingsEvent, exitEvent;
        RegisteredWaitHandle settingsWait, exitWait;
        public void InitializeEvents()
        {
            settingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\LiteClock.ShowSettings.v1");
            exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\LiteClock.Exit.v1");
            settingsWait = ThreadPool.RegisterWaitForSingleObject(settingsEvent, delegate { Dispatcher.BeginInvoke(new Action(ShowSettings)); }, null, -1, false);
            exitWait = ThreadPool.RegisterWaitForSingleObject(exitEvent, delegate { Dispatcher.BeginInvoke(new Action(Shutdown)); }, null, -1, false);
        }
        public void Start(bool openSettings)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Clock = new ClockWindow(this, Store.Load()); MainWindow = Clock; Clock.Show();
            tray = new Forms.NotifyIcon { Text = "轻时钟 · 右键设置，拖动左边缘调宽", Icon = MakeIcon(), Visible = true };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("设置 / 调整宽度…", null, delegate { ShowSettings(); });
            menu.Items.Add("回到初始位置", null, delegate { Clock.ResetPosition(); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出轻时钟", null, delegate { Shutdown(); });
            tray.ContextMenuStrip = menu; tray.DoubleClick += delegate { ShowSettings(); };
            if (Store.RecoveryMessage != null) tray.ShowBalloonTip(8000, "轻时钟", Store.RecoveryMessage, Forms.ToolTipIcon.Warning);
            if (openSettings) ShowSettings();
        }
        static Drawing.Icon MakeIcon()
        {
            using (Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("LiteClock.AppIcon"))
            using (var icon = new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize))
            {
                return (Drawing.Icon)icon.Clone();
            }
        }
        public void ShowSettings()
        {
            if (Editor == null)
            {
                Editor = new SettingsWindow(this); Editor.Closed += delegate { Editor = null; };
                Editor.Show();
            }
            if (Editor.WindowState == WindowState.Minimized) Editor.WindowState = WindowState.Normal;
            Editor.Activate();
        }
        public static bool StartupEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                return key != null && String.Equals(key.GetValue("LiteClock") as string, "\"" + ExePath + "\"", StringComparison.OrdinalIgnoreCase);
        }
        public static void SetStartup(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (enabled) key.SetValue("LiteClock", "\"" + ExePath + "\"");
                else key.DeleteValue("LiteClock", false);
            }
        }
        protected override void OnExit(ExitEventArgs e)
        {
            if (Clock != null) Clock.Stop();
            if (tray != null) { tray.Visible = false; var icon = tray.Icon; tray.Dispose(); icon.Dispose(); }
            if (settingsWait != null) settingsWait.Unregister(null);
            if (exitWait != null) exitWait.Unregister(null);
            if (settingsEvent != null) settingsEvent.Dispose();
            if (exitEvent != null) exitEvent.Dispose();
            base.OnExit(e);
        }
    }

    public sealed class ClockWindow : Window
    {
        public Settings Config;
        readonly ClockApp app;
        readonly Border surface;
        readonly StackPanel textPanel;
        readonly List<TextBlock> labels = new List<TextBlock>();
        readonly DispatcherTimer timer;
        readonly DispatcherTimer clickTimer;
        IntPtr hwnd;
        double scaleX = 1, scaleY = 1;
        bool draggingWidth, draggingPosition;
        Native.Point dragOrigin;
        Settings dragSettings;
        string lastText;
        bool skipMouseUp;
        DateTime statusCheck = DateTime.MinValue;
        DateTime positionCheck = DateTime.MinValue;
        Drawing.Rectangle lastScreen;
        public ClockWindow(ClockApp owner, Settings settings)
        {
            app = owner; Config = settings;
            Title = "轻时钟 LiteClock"; Icon = ClockApp.WindowIcon; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < 8; i++)
            {
                var line = new TextBlock { TextWrapping = TextWrapping.NoWrap, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
                TextOptions.SetTextFormattingMode(line, TextFormattingMode.Display); TextOptions.SetTextRenderingMode(line, TextRenderingMode.Grayscale);
                labels.Add(line); textPanel.Children.Add(line);
            }
            surface = new Border { Child = textPanel, ClipToBounds = true }; Content = surface;
            MouseEnter += delegate { UpdateColors(); }; MouseLeave += delegate { UpdateColors(); };
            var menu = new ContextMenu();
            AddMenu(menu, "设置 / 调整宽度…", delegate { app.ShowSettings(); });
            var widths = new MenuItem { Header = "快速设置宽度" };
            foreach (int value in new[] { 48, 64, 68, 80, 100, 120, 160, 240 })
            {
                int width = value; var item = new MenuItem { Header = width + "" };
                item.Click += delegate { ChangeWidth(width); }; widths.Items.Add(item);
            }
            menu.Items.Add(widths);
            AddMenu(menu, "回到初始位置", delegate { ResetPosition(); });
            menu.Items.Add(new Separator()); AddMenu(menu, "退出轻时钟", delegate { app.Shutdown(); });
            ContextMenu = menu;
            SourceInitialized += delegate
            {
                hwnd = new WindowInteropHelper(this).Handle;
                Native.SetWindowLong(hwnd, -20, Native.GetWindowLong(hwnd, -20) | 0x08000000 | 0x80);
                HwndSource.FromHwnd(hwnd).AddHook(Hook);
                var transform = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
                scaleX = transform.M11; scaleY = transform.M22;
                Apply(Config);
            };
            MouseLeftButtonDown += BeginDrag; MouseMove += DuringDrag; MouseLeftButtonUp += EndDrag;
            MouseDown += delegate(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Middle) { DoAction(Config.MiddleClickAction); e.Handled = true; } };
            LostMouseCapture += delegate { FinishDrag(); };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += delegate { Tick(); }; timer.Start();
            clickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Forms.SystemInformation.DoubleClickTime) };
            clickTimer.Tick += delegate { clickTimer.Stop(); DoAction(Config.ClickAction); };
        }
        static void AddMenu(ContextMenu menu, string title, Action action)
        {
            var item = new MenuItem { Header = title }; item.Click += delegate { action(); }; menu.Items.Add(item);
        }
        IntPtr Hook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == 0x21) { handled = true; return new IntPtr(3); } // WM_MOUSEACTIVATE / MA_NOACTIVATE
            return IntPtr.Zero;
        }
        public void Stop() { timer.Stop(); clickTimer.Stop(); Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged; }
        void DisplayChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate { PositionClock(); }));
        }
        Forms.Screen CurrentScreen()
        {
            return Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == Config.Monitor) ?? Forms.Screen.PrimaryScreen;
        }
        public void Apply(Settings value)
        {
            value.Validate(); Config = value.Copy();
            timer.Interval = TimeSpan.FromMilliseconds(Config.RefreshMilliseconds);
            textPanel.VerticalAlignment = (VerticalAlignment)Enum.Parse(typeof(VerticalAlignment), Config.Vertical);
            textPanel.RenderTransform = new TranslateTransform(Config.OffsetX, Config.OffsetY);
            textPanel.Opacity = Config.TextOpacity / 100;
            surface.Padding = new Thickness(Config.PaddingLeft, Config.PaddingTop, Config.PaddingRight, Config.PaddingBottom);
            surface.CornerRadius = new CornerRadius(Config.Radius); surface.BorderThickness = new Thickness(Config.BorderWidth);
            surface.BorderBrush = ColorBrush(Config.BorderColor, Config.BorderOpacity);
            surface.ToolTip = Config.TooltipEnabled ? Config.Tooltip : null;
            Topmost = Config.AlwaysOnTop;
            if (hwnd != IntPtr.Zero)
            {
                int style = Native.GetWindowLong(hwnd, -20);
                Native.SetWindowLong(hwnd, -20, Config.ClickThrough ? style | 0x20 : style & ~0x20);
            }
            for (int i = 0; i < labels.Count; i++)
            {
                LineStyle line = Config.Lines[i]; TextBlock label = labels[i];
                label.FontFamily = new FontFamily(line.Custom ? line.FontFamily : Config.FontFamily);
                label.FontSize = (line.Custom ? line.FontPoints : Config.FontPoints) * 96 / 72;
                label.FontWeight = (line.Custom ? line.Bold : Config.Bold) ? FontWeights.Bold : FontWeights.Normal;
                label.FontStyle = (line.Custom ? line.Italic : Config.Italic) ? FontStyles.Italic : FontStyles.Normal;
                label.TextDecorations = (line.Custom ? line.Underline : Config.Underline) ? TextDecorations.Underline : null;
                label.TextAlignment = (TextAlignment)Enum.Parse(typeof(TextAlignment), line.Custom ? line.Alignment : Config.Alignment);
                label.Height = line.Custom ? line.Height : Config.LineHeight; label.LineHeight = label.Height;
                label.Margin = new Thickness(0, 0, 0, Config.LineGap);
                label.RenderTransform = new TranslateTransform(line.Custom ? line.OffsetX : 0, line.Custom ? line.OffsetY : 0);
            }
            UpdateColors(); Opacity = Config.WindowOpacity / 100;
            lastText = null; UpdateText(); PositionClock();
        }
        static SolidColorBrush ColorBrush(string hex, double opacity)
        {
            Color color = Settings.ParseColor(hex); color.A = (byte)Math.Round(opacity * 255 / 100); return new SolidColorBrush(color);
        }
        void UpdateColors()
        {
            bool hover = Config.HoverEnabled && IsMouseOver;
            surface.Background = ColorBrush(hover ? Config.HoverBackground : Config.Background, Config.BackgroundOpacity);
            for (int i = 0; i < labels.Count; i++) labels[i].Foreground = ColorBrush(hover ? Config.HoverForeground : Config.Lines[i].Custom ? Config.Lines[i].Color : Config.Foreground, 100);
        }
        void UpdateText()
        {
            string text = ClockFormat.Render(Config.Format, Config.DisplayTime(DateTime.UtcNow), CultureInfo.GetCultureInfo(Config.Culture));
            if (text != lastText)
            {
                string[] lines = text.Split('\n');
                int last = -1;
                for (int i = 0; i < labels.Count; i++)
                {
                    bool shown = i < lines.Length && Config.Lines[i].Enabled;
                    labels[i].Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
                    labels[i].Text = i < lines.Length ? lines[i] : "";
                    labels[i].Margin = new Thickness(0, 0, 0, Config.LineGap); if (shown) last = i;
                }
                if (last >= 0) labels[last].Margin = new Thickness(0);
                lastText = text;
            }
        }
        public void PositionClock()
        {
            if (hwnd == IntPtr.Zero) return;
            Drawing.Rectangle screen = CurrentScreen().Bounds; lastScreen = screen;
            int width = Math.Max(24, Math.Min((int)Math.Round(Config.Width * scaleX), screen.Width));
            int height = Math.Max(24, Math.Min((int)Math.Round(Config.Height * scaleY), screen.Height));
            int x = Math.Max(screen.Left, screen.Right - width - (int)Math.Round(Config.Right * scaleX));
            int y = Math.Max(screen.Top, screen.Bottom - height - (int)Math.Round(Config.Bottom * scaleY));
            Width = width / scaleX; Height = height / scaleY;
            Native.SetWindowPos(hwnd, new IntPtr(Config.AlwaysOnTop ? -1 : -2), x, y, width, height, 0x10 | 0x200);
        }
        void Tick()
        {
            UpdateText();
            DateTime now = DateTime.UtcNow;
            if ((now - statusCheck).TotalSeconds < 1) return;
            statusCheck = now;
            Opacity = Config.HideFullscreen && app.Editor == null && !IsMouseCaptured && Native.IsFullscreen(hwnd, lastScreen) ? 0 : Config.WindowOpacity / 100;
            if ((now - positionCheck).TotalSeconds >= 2) { positionCheck = now; PositionClock(); }
        }
        void BeginDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { clickTimer.Stop(); skipMouseUp = true; DoAction(Config.DoubleClickAction); e.Handled = true; return; }
            if (Config.LockPosition || app.Editor != null) return;
            draggingPosition = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            draggingWidth = !draggingPosition && e.GetPosition(this).X <= 7;
            if (!draggingPosition && !draggingWidth) return;
            Native.GetCursorPos(out dragOrigin); dragSettings = Config.Copy(); CaptureMouse(); e.Handled = true;
        }
        void DuringDrag(object sender, MouseEventArgs e)
        {
            if (!IsMouseCaptured)
            {
                Cursor = !Config.LockPosition && app.Editor == null && e.GetPosition(this).X <= 7 ? Cursors.SizeWE : Cursors.Arrow; return;
            }
            Native.Point point; Native.GetCursorPos(out point);
            double dx = (point.X - dragOrigin.X) / scaleX, dy = (point.Y - dragOrigin.Y) / scaleY;
            if (draggingWidth) Config.Width = Math.Round(Math.Max(24, Math.Min(Math.Min(1200, lastScreen.Width / scaleX - Config.Right), dragSettings.Width - dx)));
            if (draggingPosition)
            {
                Config.Right = Math.Round(Math.Max(0, Math.Min(lastScreen.Width / scaleX - Width, dragSettings.Right - dx)));
                Config.Bottom = Math.Round(Math.Max(0, Math.Min(lastScreen.Height / scaleY - Height, dragSettings.Bottom - dy)));
            }
            PositionClock();
        }
        void EndDrag(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured) { ReleaseMouseCapture(); return; }
            if (skipMouseUp) { skipMouseUp = false; return; }
            clickTimer.Stop(); clickTimer.Start();
        }
        void DoAction(string action)
        {
            if (action == "Settings") app.ShowSettings();
            else if (action == "Copy")
            {
                try { Clipboard.SetText(lastText ?? ""); } catch (Exception ex) { Store.Log(ex); }
            }
            else if (action == "Calendar")
            {
                var date = Config.DisplayTime(DateTime.UtcNow);
                var calendar = new System.Windows.Controls.Calendar { SelectedDate = date.Date, DisplayDate = date.Date, Margin = new Thickness(16) };
                var window = new Window { Title = "轻时钟 · 日历", Icon = ClockApp.WindowIcon, Content = calendar, SizeToContent = SizeToContent.WidthAndHeight,
                    ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = Config.AlwaysOnTop };
                window.Show();
            }
        }
        void FinishDrag()
        {
            if (!draggingWidth && !draggingPosition) return;
            draggingWidth = draggingPosition = false; SaveInteractive();
        }
        void SaveInteractive()
        {
            try { Store.Save(Config); }
            catch (Exception ex) { Store.Log(ex); MessageBox.Show("未能保存设置：" + ex.Message, "轻时钟"); }
        }
        public void ChangeWidth(int width)
        {
            if (app.Editor != null) { app.ShowSettings(); return; }
            Config.Width = width; PositionClock(); SaveInteractive();
        }
        public void ResetPosition()
        {
            if (app.Editor != null) { app.ShowSettings(); return; }
            Config.Right = 0; Config.Bottom = 56; Config.Monitor = ""; PositionClock(); SaveInteractive();
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Contains("--self-test")) return SelfTest(args);
            bool created;
            using (var mutex = new Mutex(true, "Local\\LiteClock.SingleInstance.v1", out created))
            {
                if (args.Contains("--exit")) { Signal("Local\\LiteClock.Exit.v1"); return 0; }
                if (!created) { Signal("Local\\LiteClock.ShowSettings.v1"); return 0; }
                try
                {
                    var app = new ClockApp();
                    app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
                    {
                        Store.Log(e.Exception); e.Handled = true;
                        MessageBox.Show("轻时钟遇到错误，详情已写入程序目录的 LiteClock.log。\n" + e.Exception.Message, "轻时钟"); app.Shutdown(1);
                    };
                    app.InitializeEvents(); app.Start(args.Contains("--settings")); return app.Run();
                }
                catch (Exception ex) { Store.Log(ex); MessageBox.Show(ex.Message, "轻时钟启动失败"); return 1; }
                finally { mutex.ReleaseMutex(); }
            }
        }
        static void Signal(string name)
        {
            try { using (var handle = EventWaitHandle.OpenExisting(name)) handle.Set(); } catch (WaitHandleCannotBeOpenedException) { }
        }
        static int SelfTest(string[] args)
        {
            string report = args.Length > 1 ? args[1] : Path.Combine(Store.DirectoryPath, "self-test.txt");
            var lines = new List<string>();
            Action<bool, string> check = delegate(bool condition, string label) { if (!condition) throw new Exception("FAIL: " + label); lines.Add("PASS: " + label); };
            string previous = Store.DirectoryPath;
            string scratch = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report)), "LiteClock-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var culture = CultureInfo.GetCultureInfo("zh-SG");
                check(ClockFormat.Render("%H:%M:%S\r\n%m-%d\r\n%a", new DateTime(2026, 10, 5, 9, 8, 7), culture) == "09:08:07\n10-05\n周一", "imported three-line format and weekday");
                check(ClockFormat.Render("%F %T %j %%", new DateTime(2024, 2, 29, 0, 0, 0), culture) == "2024-02-29 00:00:00 060 %", "leap day, midnight, escaped percent");
                check(ClockFormat.Render("%F %a", new DateTime(2026, 12, 31, 23, 59, 59).AddSeconds(1), culture) == "2027-01-01 周五", "year rollover");
                bool rejected = false; try { ClockFormat.Render("%Q", DateTime.Now, culture); } catch (ArgumentException) { rejected = true; }
                check(rejected, "unsupported format rejected");
                var settings = new Settings(); settings.Validate();
                settings.Width = Double.NaN; rejected = false; try { settings.Validate(); } catch (ArgumentException) { rejected = true; }
                check(rejected, "invalid dimensions rejected");
                settings = new Settings(); settings.Lines[0].Custom = true; settings.Lines[0].FontPoints = 16;
                var copied = settings.Copy(); copied.Lines[0].FontPoints = 20;
                check(settings.Lines[0].FontPoints == 16, "preview uses independent per-line copy");
                settings = new Settings { TimeZone = "UTC", TimeOffsetMinutes = -60 };
                check(settings.DisplayTime(new DateTime(2026, 1, 1, 0, 30, 0, DateTimeKind.Utc)) == new DateTime(2025, 12, 31, 23, 30, 0), "timezone and offset cross midnight correctly");
                settings = new Settings { TimeZone = "Pacific Standard Time" };
                check(settings.DisplayTime(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc)).Hour == 5 && settings.DisplayTime(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)).Hour == 4, "timezone daylight saving rules");
                settings = new Settings { Format = "%n%n%n%n%n%n%n%n" };
                rejected = false; try { settings.Validate(); } catch (ArgumentException) { rejected = true; }
                check(rejected, "escaped newlines respect eight-line limit");
                settings = new Settings { Foreground = "bad color" };
                rejected = false; try { settings.Validate(); } catch (ArgumentException) { rejected = true; }
                check(rejected, "invalid colors rejected");
                Directory.CreateDirectory(scratch); Store.DirectoryPath = scratch;
                settings = new Settings { Width = 123, Radius = 7, ClickAction = "Calendar" };
                settings.Lines[1].Custom = true; settings.Lines[1].Color = "#123456"; Store.Save(settings);
                check(Store.Load().Width == 123 && Store.Load().Lines[1].Color == "#123456" && Store.Load().Radius == 7 && Store.Load().ClickAction == "Calendar", "all custom settings round-trip");
                settings.Width = 217; Store.Save(settings);
                check(Store.Load().Width == 217 && File.Exists(Store.ConfigPath + ".bak"), "atomic save and backup");
                File.WriteAllText(Store.ConfigPath, "broken json");
                check(Store.Load().Width == 123 && Store.RecoveryMessage != null, "corrupt settings recover from backup");
                File.Delete(Store.ConfigPath + ".bak");
                check(Store.Load().Width == 68, "corrupt settings fallback");
                File.Delete(Store.ConfigPath); Store.RecoveryMessage = null;
                Settings firstRun = Store.Load(); firstRun.Validate();
                check(firstRun.Format == "%H:%M:%S\n%m-%d\n%a" && Store.RecoveryMessage == null,
                    "first launch uses defaults without a personal configuration file");
                lines.Add("ALL TESTS PASSED"); File.WriteAllLines(report, lines, Encoding.UTF8); return 0;
            }
            catch (Exception ex) { lines.Add(ex.ToString()); File.WriteAllLines(report, lines, Encoding.UTF8); return 1; }
            finally
            {
                Store.DirectoryPath = previous;
                // Scratch is a fresh, uniquely named directory created by this test.
                if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
            }
        }
    }
}
