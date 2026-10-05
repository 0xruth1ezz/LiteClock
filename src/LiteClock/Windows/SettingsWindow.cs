using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.UI.Text;
using FontWeights = Microsoft.UI.Text.FontWeights;

namespace LiteClock
{
    // A draft is applied to both WinUI ClockViews. Closing restores the saved baseline.
    public sealed class SettingsWindow : Window
    {
        sealed class FieldBinding
        {
            public string Key, Title;
            public FrameworkElement Editor;
            public PropertyInfo Property;
            public object Read()
            {
                if (Editor is ToggleSwitch toggle) return toggle.IsOn;
                if (Editor is NumberInput number)
                {
                    double value = number.ReadValue();
                    return Property.PropertyType == typeof(int) ? (object)(int)value : value;
                }
                if (Editor is ComboBox combo)
                {
                    var selected = combo.SelectedItem as ComboBoxItem;
                    return combo.IsEditable && (selected == null || combo.Text != Convert.ToString(selected.Content))
                        ? combo.Text : Convert.ToString(selected?.Tag);
                }
                // WinUI multiline TextBox uses CR, while stored formats use LF.
                return ((TextBox)Editor).Text.Replace("\r\n", "\n").Replace('\r', '\n');
            }
            public void Load(object source)
            {
                object value = Property.GetValue(source);
                if (Editor is ToggleSwitch toggle) { toggle.IsOn = (bool)value; return; }
                if (Editor is NumberInput number) { number.SetValue(Convert.ToDouble(value, CultureInfo.InvariantCulture)); return; }
                string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (Editor is not ComboBox combo) { ((TextBox)Editor).Text = text; return; }
                var item = combo.Items.Cast<ComboBoxItem>().FirstOrDefault(x => (string)x.Tag == text);
                if (item == null && !combo.IsEditable) { item = new ComboBoxItem { Content = text, Tag = text }; combo.Items.Add(item); }
                combo.SelectedItem = item;
                if (combo.IsEditable) combo.Text = item == null ? text : Convert.ToString(item.Content);
            }
        }
        readonly ClockApp app;
        Settings baseline, draft;
        readonly List<FieldBinding> fields = new(), lineFields = new();
        readonly ToggleSwitch startup;
        readonly ComboBox lineSelector;
        readonly InfoBar status;
        readonly TextBlock previewCaption;
        readonly ClockView preview = new();
        readonly Viewbox previewBox;
        readonly DispatcherTimer previewTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
        bool loading = true, picking, closed;
        int lineIndex;
        public SettingsWindow(ClockApp owner)
        {
            app = owner; baseline = app.Clock.Config.Copy(); draft = baseline.Copy();
            Title = AppDetails.WindowTitle("自定义设置");
            Native.Center(this, 800, 900);
            Native.MinimumSize(this, 680, 540);
            var root = new Grid { Padding = new Thickness(24, 18, 24, 20), RowSpacing = 16, Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"] };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            root.Children.Add(header);
            var intro = new StackPanel { Spacing = 6 }; header.Children.Add(intro);
            var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            brand.Children.Add(new Image { Source = new BitmapImage(new Uri("ms-appx:///LiteClock.png")), Width = 40, Height = 40 });
            brand.Children.Add(new TextBlock { Text = "轻时钟", FontSize = 28, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            intro.Children.Add(brand);
            intro.Children.Add(new TextBlock { Text = "内容、布局、外观和操作，都由你调整。", Opacity = 0.7 });
            previewBox = new Viewbox { Child = preview, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Right };
            var card = new StackPanel { Margin = new Thickness(16, 0, 0, 0), Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
            ToolTipService.SetToolTip(card, "与桌面时钟使用相同的 WinUI 3 渲染组件；尺寸较大时等比缩小。");
            card.Children.Add(previewBox);
            previewCaption = new TextBlock { FontSize = 11, Opacity = 0.7, TextAlignment = TextAlignment.Right };
            card.Children.Add(previewCaption); Grid.SetColumn(card, 1); header.Children.Add(card);

            var footer = new StackPanel { Spacing = 12 }; Grid.SetRow(footer, 2); root.Children.Add(footer);
            status = new InfoBar { IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Informational, Message = "设置会实时预览到时钟；保存后保留。" };
            footer.Children.Add(status);
            var buttonRow = new Grid(); buttonRow.ColumnDefinitions.Add(new ColumnDefinition()); buttonRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); footer.Children.Add(buttonRow);
            var files = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }; buttonRow.Children.Add(files);
            files.Children.Add(MakeButton("导入", Import)); files.Children.Add(MakeButton("导出", Export));
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }; Grid.SetColumn(actions, 1); buttonRow.Children.Add(actions);
            actions.Children.Add(MakeButton("取消", Close)); actions.Children.Add(MakeButton("保存", () => Save(false)));
            var done = MakeButton("保存并关闭", () => Save(true)); done.Style = (Style)Application.Current.Resources["AccentButtonStyle"]; actions.Children.Add(done);
            var tabs = new NavigationView { PaneDisplayMode = NavigationViewPaneDisplayMode.Top, IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed, IsSettingsVisible = false, IsPaneToggleButtonVisible = false, AlwaysShowHeader = false };
            Grid.SetRow(tabs, 1); root.Children.Add(tabs);
            tabs.SelectionChanged += (_, e) => { if (e.SelectedItem is NavigationViewItem item) tabs.Content = item.Tag; };
            StackPanel content = Page(tabs, "内容");
            Section(content, "日期与时间格式");
            Hint(content, "一行格式对应一行显示，可以直接加入固定文字。现有 JSON 配置可直接导入。");
            Multi(content, "Format", "显示格式", 110, false);
            Hint(content, "%H:%M:%S  时:分:秒    %m-%d  月-日    %a  周几\n%Y 年   %A 完整星期   %I 12小时   %p 上午/下午\n%F 年-月-日   %T 时:分:秒   %b/%B 月名\n%j 年内第几天   %w 星期编号   %% 百分号\n最多 8 行；不解析 HTML，也不执行代码。");
            Choice(content, "Culture", "日期语言", new[] { "zh-SG|中文（周一）", "zh-CN|中文（周一）", "zh-TW|繁體中文", "en-US|English" }, true, false);
            var zones = new List<string> { "Local|跟随 Windows 系统时区" };
            zones.AddRange(TimeZoneInfo.GetSystemTimeZones().Select(z => z.Id + "|" + z.DisplayName));
            Choice(content, "TimeZone", "时区", zones.ToArray(), false, false);
            Field(content, "TimeOffsetMinutes", "时间偏移（分钟）", "-10080–10080；可用小数");
            Field(content, "RefreshMilliseconds", "刷新间隔（毫秒）", "100–60000；250 可及时更新秒数");
            Hint(content, "使用 Windows 系统时间。指定时区会自动遵循该时区的夏令时规则。");

            StackPanel layout = Page(tabs, "布局");
            Section(layout, "窗口尺寸");
            Field(layout, "Width", "宽度", "24–1200；左边缘拖动也可以调宽");
            Field(layout, "Height", "高度", "24–600");
            Section(layout, "屏幕位置");
            Field(layout, "Right", "距屏幕右侧", "0–20000；增大后向左移动");
            Field(layout, "Bottom", "距屏幕底部", "0–20000；增大后向上移动");
            var monitors = new List<string> { "|主显示器（自动跟随）" };
            monitors.AddRange(Native.Monitors().Select(s => s.DeviceName + "|" + s.DeviceName + "  " + s.Bounds.Width + " × " + s.Bounds.Height));
            Choice(layout, "Monitor", "显示器", monitors.ToArray(), false, false);
            Hint(layout, "尺寸按 Windows 逻辑像素计算。关闭设置窗口后，拖动时钟左侧 7 像素区域可调整宽度；Shift + 拖动可移动。");
            Section(layout, "文字布局");
            Choice(layout, "Alignment", "水平对齐", new[] { "Left|左对齐", "Center|居中", "Right|右对齐" }, false, false);
            Choice(layout, "Vertical", "垂直对齐", new[] { "Top|顶部", "Center|居中", "Bottom|底部" }, false, false);
            Field(layout, "PaddingLeft", "左内边距", "0–300"); Field(layout, "PaddingRight", "右内边距", "0–300");
            Field(layout, "PaddingTop", "上内边距", "0–300"); Field(layout, "PaddingBottom", "下内边距", "0–300");
            Field(layout, "OffsetX", "文字水平偏移", "-500–500；正数向右"); Field(layout, "OffsetY", "文字垂直偏移", "-500–500；正数向下");
            Field(layout, "LineHeight", "默认行高", "8–120"); Field(layout, "LineGap", "额外行间距", "0–100");

            StackPanel text = Page(tabs, "文字");
            Section(text, "默认文字样式");
            string[] fonts = InstalledFonts();
            Choice(text, "FontFamily", "字体", fonts, true, false);
            Field(text, "FontPoints", "字号（pt）", "5–72");
            Toggle(text, "Bold", "加粗", false); Toggle(text, "Italic", "斜体", false); Toggle(text, "Underline", "下划线", false);
            ColorField(text, "Foreground", "默认文字颜色", false);
            Field(text, "TextOpacity", "文字不透明度 %", "0–100");
            Section(text, "逐行自定义");
            Hint(text, "每一行可以继承默认样式，也可以单独设置。行号对应“内容”页中格式的行号。");
            lineSelector = CreateComboBox(false);
            lineSelector.Margin = new Thickness(0, 5, 0, 10);
            for (int i = 1; i <= 8; i++) lineSelector.Items.Add("第 " + i + " 行");
            lineSelector.SelectedIndex = 0; text.Children.Add(lineSelector);
            Toggle(text, "Enabled", "显示这一行", true); Toggle(text, "Custom", "这一行使用独立样式", true);
            Choice(text, "FontFamily", "字体", fonts, true, true);
            Field(text, "FontPoints", "字号（pt）", "5–72", true);
            Toggle(text, "Bold", "加粗", true); Toggle(text, "Italic", "斜体", true); Toggle(text, "Underline", "下划线", true);
            ColorField(text, "Color", "文字颜色", true);
            Choice(text, "Alignment", "水平对齐", new[] { "Left|左对齐", "Center|居中", "Right|右对齐" }, false, true);
            Field(text, "Height", "这一行高度", "8–160", true);
            Field(text, "OffsetX", "水平偏移", "-500–500", true); Field(text, "OffsetY", "垂直偏移", "-500–500", true);
            lineSelector.SelectionChanged += SwitchLine;

            StackPanel appearance = Page(tabs, "外观");
            Section(appearance, "背景与透明度");
            ColorField(appearance, "Background", "背景颜色", false);
            Field(appearance, "BackgroundOpacity", "背景不透明度 %", "0–100；文字透明度在“文字”页设置");
            Field(appearance, "WindowOpacity", "整体不透明度 %", "5–100；对整个时钟生效");
            Field(appearance, "Radius", "圆角半径", "0–300；0 为直角");
            Section(appearance, "边框");
            Field(appearance, "BorderWidth", "边框宽度", "0–30；0 为无边框");
            ColorField(appearance, "BorderColor", "边框颜色", false);
            Field(appearance, "BorderOpacity", "边框不透明度 %", "0–100");
            Section(appearance, "鼠标悬停");
            Toggle(appearance, "HoverEnabled", "鼠标悬停时改变颜色", false);
            ColorField(appearance, "HoverBackground", "悬停背景颜色", false); ColorField(appearance, "HoverForeground", "悬停文字颜色", false);

            StackPanel behavior = Page(tabs, "操作");
            Section(behavior, "系统启动");
            startup = new ToggleSwitch
            {
                Header = "随系统启动", OnContent = "已开启", OffContent = "已关闭",
                Margin = new Thickness(0, 6, 0, 8)
            };
            AutomationProperties.SetName(startup, "随系统启动");
            behavior.Children.Add(startup);
            Hint(behavior, "登录 Windows 后自动显示轻时钟。点击“保存”后生效；取消或关闭设置不会更改启动项。");
            Section(behavior, "窗口行为");
            Toggle(behavior, "AlwaysOnTop", "保持置顶", false); Toggle(behavior, "HideFullscreen", "全屏应用前台时隐藏", false);
            Toggle(behavior, "LockPosition", "锁定位置和边缘拖动", false); Toggle(behavior, "ClickThrough", "鼠标穿透时钟", false);
            Hint(behavior, "开启鼠标穿透后，从系统托盘图标打开设置，或再次运行 LiteClock.exe。");
            Section(behavior, "点击动作");
            string[] actionsList = { "None|不执行动作", "Settings|打开设置", "Calendar|打开日历", "Copy|复制当前显示文字" };
            Choice(behavior, "ClickAction", "单击", actionsList, false, false);
            Choice(behavior, "DoubleClickAction", "双击", actionsList, false, false);
            Choice(behavior, "MiddleClickAction", "中键", actionsList, false, false);
            Hint(behavior, "右键保留设置菜单，避免更改点击动作后无法进入设置。");
            Section(behavior, "提示文字");
            Toggle(behavior, "TooltipEnabled", "显示鼠标悬停提示", false); Multi(behavior, "Tooltip", "提示内容", 80, false);
            Section(behavior, "配置");
            Button reset = MakeButton("恢复默认样式", delegate { draft = new Settings(); LoadAll(); QueuePreview(null, null); });
            reset.HorizontalAlignment = HorizontalAlignment.Left; behavior.Children.Add(reset);
            Hint(behavior, "导入、导出包括全部外观和操作设置。配置保存在程序旁的 settings.json；保存时保留上一个版本作为备份。");

            AddAppInformation(Page(tabs, "应用信息"));

            tabs.Content = ((NavigationViewItem)tabs.MenuItems[0]).Tag;
            tabs.Loaded += (_, _) => { if (tabs.SelectedItem == null) tabs.SelectedItem = tabs.MenuItems[0]; };
            previewTimer.Tick += (_, _) => { previewTimer.Stop(); Preview(); };
            app.Clock.ConfigurationChanged += ClockConfigurationChanged;
            app.Clock.TimeChanged += ClockTimeChanged;
            LoadAll(); startup.IsOn = ClockApp.StartupEnabled(); loading = false; RefreshSample();
            Closed += (_, _) =>
            {
                closed = true; previewTimer.Stop();
                app.Clock.ConfigurationChanged -= ClockConfigurationChanged;
                app.Clock.TimeChanged -= ClockTimeChanged;
                if (!app.IsShuttingDown) app.Clock.Apply(baseline);
            };
        }
        static string[] InstalledFonts()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Microsoft YaHei UI", "Segoe UI", "Segoe UI Variable", "Consolas", "Arial" };
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                using var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
                if (key == null) continue;
                foreach (string name in key.GetValueNames())
                {
                    var clean = System.Text.RegularExpressions.Regex.Replace(name, @"\s*\([^)]*\)$", "");
                    foreach (var family in clean.Split(" & ")) names.Add(family);
                }
            }
            return names.OrderBy(x => x).Select(x => x + "|" + x).ToArray();
        }
        void AddAppInformation(StackPanel panel)
        {
            var identity = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Margin = new Thickness(0, 16, 0, 12) };
            identity.Children.Add(new Image { Source = new BitmapImage(new Uri("ms-appx:///LiteClock.png")), Width = 56, Height = 56 });
            var name = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(new TextBlock { Text = AppDetails.Name, FontSize = 24, FontWeight = FontWeights.SemiBold });
            name.Children.Add(new TextBlock { Text = AppDetails.Description, TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
            identity.Children.Add(name); panel.Children.Add(identity);
            Section(panel, "版本与环境");
            foreach (var item in AppDetails.EnvironmentDetails) InformationRow(panel, item.Label, item.Value);
            Section(panel, "本机文件");
            foreach (var item in AppDetails.FileDetails) InformationRow(panel, item.Label, item.Value);
            var copy = MakeButton("复制应用信息", () =>
            {
                try
                {
                    var data = new DataPackage(); data.SetText(AppDetails.CopyText());
                    Clipboard.SetContent(data); Clipboard.Flush();
                    SetStatus("已复制应用信息（包含本机程序和配置路径）。", InfoBarSeverity.Success);
                }
                catch (Exception ex) { Store.Log(ex); Error("无法复制应用信息：" + ex.Message); }
            });
            copy.HorizontalAlignment = HorizontalAlignment.Left; copy.Margin = new Thickness(0, 16, 0, 4);
            panel.Children.Add(copy);
        }
        static void InformationRow(StackPanel panel, string label, string value)
        {
            var row = new Grid { ColumnSpacing = 16, Margin = new Thickness(0, 8, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new TextBlock { Text = label, Opacity = 0.7 });
            var text = new TextBlock { Text = value, IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };
            AutomationProperties.SetName(text, label + "：" + value);
            Grid.SetColumn(text, 1); row.Children.Add(text); panel.Children.Add(row);
        }
        static Button MakeButton(string title, Action action)
        {
            var button = new Button { Content = title }; button.Click += (_, _) => action(); return button;
        }
        static StackPanel Page(NavigationView tabs, string title)
        {
            var panel = new StackPanel { Margin = new Thickness(8, 4, 16, 20), Spacing = 6 };
            var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Disabled };
            tabs.MenuItems.Add(new NavigationViewItem { Content = title, Tag = scroll }); return panel;
        }
        static void Section(StackPanel panel, string text) => panel.Children.Add(new TextBlock { Text = text, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 6) });
        static void Hint(StackPanel panel, string text) => panel.Children.Add(new TextBlock { Text = text, FontSize = 12, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });
        void Bind(string key, string title, FrameworkElement editor, bool line)
        {
            var binding = new FieldBinding { Key = key, Title = title, Editor = editor, Property = (line ? typeof(LineStyle) : typeof(Settings)).GetProperty(key) };
            AutomationProperties.SetName(editor, title + (line ? "（当前行）" : ""));
            if (editor is NumberInput number) number.SetLabel(title + (line ? "（当前行）" : ""));
            (line ? lineFields : fields).Add(binding);
        }
        static void Row(StackPanel panel, string title, FrameworkElement editor, string hint)
        {
            var grid = new Grid { ColumnSpacing = 16, Margin = new Thickness(0, 3, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            editor.HorizontalAlignment = HorizontalAlignment.Stretch; Grid.SetColumn(editor, 1); grid.Children.Add(editor); panel.Children.Add(grid);
            if (!string.IsNullOrEmpty(hint)) Hint(panel, hint);
        }
        void Field(StackPanel panel, string key, string title, string hint, bool line = false)
        {
            var box = new NumberInput(NumberRules.For(key, line)); Bind(key, title, box, line);
            box.ValueChanged += QueuePreview; Row(panel, title, box, hint);
        }
        void Multi(StackPanel panel, string key, string title, double height, bool line)
        {
            var box = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = height, FontFamily = new FontFamily("Consolas"), Header = title };
            ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
            Bind(key, title, box, line); box.TextChanged += QueuePreview; panel.Children.Add(box);
        }
        void Toggle(StackPanel panel, string key, string title, bool line)
        {
            var box = new ToggleSwitch { Header = title, Margin = new Thickness(0, 4, 0, 6) };
            Bind(key, title, box, line); box.Toggled += QueuePreview; panel.Children.Add(box);
        }
        static ComboBox CreateComboBox(bool editable) => new() { IsEditable = editable, MaxDropDownHeight = 320, HorizontalAlignment = HorizontalAlignment.Stretch };
        void Choice(StackPanel panel, string key, string title, string[] choices, bool editable, bool line)
        {
            var combo = CreateComboBox(editable);
            foreach (string choice in choices)
            {
                int split = choice.IndexOf('|');
                combo.Items.Add(new ComboBoxItem { Tag = split < 0 ? choice : choice.Substring(0, split), Content = split < 0 ? choice : choice.Substring(split + 1) });
            }
            Bind(key, title, combo, line); combo.SelectionChanged += QueuePreview;
            if (editable) combo.RegisterPropertyChangedCallback(ComboBox.TextProperty, (_, _) => QueuePreview(null, null));
            Row(panel, title, combo, null);
        }
        void ColorField(StackPanel panel, string key, string title, bool line)
        {
            var grid = new Grid { ColumnSpacing = 8 }; grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var box = new TextBox { PlaceholderText = "#RRGGBB", MaxLength = 7 }; Bind(key, title, box, line); box.TextChanged += QueuePreview; grid.Children.Add(box);
            var picker = new ColorPicker { IsAlphaEnabled = false, IsHexInputVisible = true, IsColorPreviewVisible = true };
            var pickerPanel = new StackPanel { Spacing = 10 };
            pickerPanel.Children.Add(picker);
            var flyout = new Flyout { Content = pickerPanel };
            var eyedropper = new Button { Content = "屏幕取色", HorizontalAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(eyedropper, title + "屏幕取色");
            ToolTipService.SetToolTip(eyedropper, "从屏幕任意位置取色；左键确认，右键或 Esc 取消。");
            eyedropper.Click += async (_, _) =>
            {
                if (picking) return;
                picking = true; flyout.Hide(); previewTimer.Stop();
                try
                {
                    string color = await ScreenColorPicker.PickAsync(this);
                    if (!closed) { if (color != null) box.Text = color; Preview(); }
                }
                catch (Exception ex) { Store.Log(ex); if (!closed) Error("屏幕取色失败：" + ex.Message); }
                finally { picking = false; }
            };
            pickerPanel.Children.Add(eyedropper);
            flyout.Opening += (_, _) => { try { picker.Color = Settings.ParseColor(box.Text); } catch (ArgumentException) { } };
            picker.ColorChanged += (_, e) => box.Text = $"#{e.NewColor.R:X2}{e.NewColor.G:X2}{e.NewColor.B:X2}";
            var button = new Button { Content = "选择…", Flyout = flyout };
            AutomationProperties.SetName(button, title + "选择颜色"); Grid.SetColumn(button, 1); grid.Children.Add(button); Row(panel, title, grid, null);
        }
        void LoadAll()
        {
            bool previous = loading; loading = true;
            try { foreach (var field in fields) field.Load(draft); foreach (var field in lineFields) field.Load(draft.Lines[lineIndex]); }
            finally { loading = previous; }
        }
        void SwitchLine(object sender, SelectionChangedEventArgs e)
        {
            if (loading || lineSelector.SelectedIndex < 0) return;
            try
            {
                var value = draft.Lines[lineIndex].Copy();
                foreach (var field in lineFields) field.Property.SetValue(value, field.Read());
                value.Validate(); draft.Lines[lineIndex] = value; lineIndex = lineSelector.SelectedIndex;
                loading = true; foreach (var field in lineFields) field.Load(draft.Lines[lineIndex]);
            }
            catch (Exception ex) { Error(ex.Message); loading = true; lineSelector.SelectedIndex = lineIndex; }
            finally { loading = false; }
            QueuePreview(null, null);
        }
        internal Settings Collect()
        {
            var value = draft.Copy();
            foreach (var field in fields) field.Property.SetValue(value, field.Read());
            foreach (var field in lineFields) field.Property.SetValue(value.Lines[lineIndex], field.Read());
            value.Validate(); return value;
        }
        void QueuePreview(object sender, object e)
        {
            if (loading || closed) return; previewTimer.Stop(); previewTimer.Start();
        }
        void Preview()
        {
            try
            {
                var value = Collect(); draft = value.Copy(); app.Clock.Apply(value);
                int count = ClockFormat.Render(value.Format, value.DisplayTime(DateTime.UtcNow), CultureInfo.GetCultureInfo(value.Culture)).Split('\n').Length;
                int enabled = value.Lines.Take(count).Count(l => l.Enabled);
                double required = value.Lines.Take(count).Where(l => l.Enabled).Sum(l => l.Custom ? l.Height : value.LineHeight)
                    + Math.Max(0, enabled - 1) * value.LineGap + value.PaddingTop + value.PaddingBottom + value.BorderWidth * 2;
                SetStatus(value.Width < 56 || required > value.Height ? "当前尺寸可能裁切文字；可增大宽高，或减小字号、行高及边距。" : "正在实时预览；保存后保留，取消会还原。", InfoBarSeverity.Informational);
            }
            catch (Exception ex) { Error(ex.Message); }
        }
        void ClockConfigurationChanged(object sender, EventArgs e) => RefreshSample();
        void ClockTimeChanged(object sender, EventArgs e) => preview.UpdateTime(DateTime.UtcNow);
        void RefreshSample()
        {
            var value = app.Clock.Config; double width = value.Width, height = value.Height;
            preview.Apply(value); preview.Width = width; preview.Height = height;
            double scale = Math.Min(1, Math.Min(168 / width, 72 / height));
            previewBox.Width = width * scale; previewBox.Height = height * scale;
            previewCaption.Text = "实时预览 · " + (scale * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
            AutomationProperties.SetName(preview, $"时钟实时预览，{width:0.#} × {height:0.#}");
        }
        void SetStatus(string message, InfoBarSeverity severity) { status.Message = message; status.Severity = severity; status.IsOpen = true; }
        void Error(string message) => SetStatus(message, InfoBarSeverity.Error);
        void Save(bool close)
        {
            previewTimer.Stop();
            try
            {
                var value = Collect(); bool oldStartup = ClockApp.StartupEnabled();
                if (startup.IsOn != oldStartup) ClockApp.SetStartup(startup.IsOn);
                try { Store.Save(value); }
                catch { if (startup.IsOn != oldStartup) ClockApp.SetStartup(oldStartup); throw; }
                baseline = value.Copy(); draft = value.Copy(); app.Clock.Apply(value);
                SetStatus("已保存。", InfoBarSeverity.Success); if (close) Close();
            }
            catch (Exception ex) { Store.Log(ex); Error("保存未完成：" + ex.Message); }
        }
        async void Import()
        {
            if (picking) return; picking = true;
            try
            {
                var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".json");
                WinRT.Interop.InitializeWithWindow.Initialize(picker, Native.Handle(this));
                var file = await picker.PickSingleFileAsync();
                if (file == null || closed) return;
                draft = Store.Read(file.Path); LoadAll(); Preview();
            }
            catch (Exception ex) { if (!closed) Error("无法导入：" + ex.Message); }
            finally { picking = false; }
        }
        async void Export()
        {
            if (picking) return; picking = true;
            try
            {
                var value = Collect();
                var picker = new FileSavePicker { SuggestedFileName = "LiteClock-theme" };
                picker.FileTypeChoices.Add("轻时钟配置", new List<string> { ".json" });
                WinRT.Interop.InitializeWithWindow.Initialize(picker, Native.Handle(this));
                var file = await picker.PickSaveFileAsync();
                if (file == null || closed) return;
                File.WriteAllText(file.Path, Store.Serialize(value)); SetStatus("已导出当前配置。", InfoBarSeverity.Success);
            }
            catch (Exception ex) { if (!closed) Error("无法导出：" + ex.Message); }
            finally { picking = false; }
        }
    }
}
