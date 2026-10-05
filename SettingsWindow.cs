using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace LiteClock
{
    // Every editor changes a draft. Closing or cancelling restores the last saved state.
    public sealed class SettingsWindow : Window
    {
        sealed class FieldBinding
        {
            public string Key, Title;
            public FrameworkElement Editor;
            public PropertyInfo Property;
            public object Read()
            {
                if (Editor is CheckBox) return ((CheckBox)Editor).IsChecked == true;
                var number = Editor as NumberInput;
                if (number != null)
                {
                    double value = number.ReadValue();
                    return Property.PropertyType == typeof(int) ? (object)(int)value : value;
                }
                string text;
                var combo = Editor as ComboBox;
                if (combo != null)
                {
                    var selected = combo.SelectedItem as ComboBoxItem;
                    text = combo.IsEditable && (selected == null || combo.Text != Convert.ToString(selected.Content))
                        ? combo.Text : Convert.ToString(selected.Tag);
                }
                else text = ((TextBox)Editor).Text;
                if (Property.PropertyType == typeof(double))
                {
                    double result;
                    if (!Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) throw new ArgumentException(Title + "：请填写数字，小数用英文句点。");
                    return result;
                }
                if (Property.PropertyType == typeof(int))
                {
                    int result;
                    if (!Int32.TryParse(text, out result)) throw new ArgumentException(Title + "：请填写整数。");
                    return result;
                }
                return text;
            }
            public void Load(object source)
            {
                object value = Property.GetValue(source, null);
                if (Editor is CheckBox) { ((CheckBox)Editor).IsChecked = (bool)value; return; }
                var number = Editor as NumberInput;
                if (number != null) { number.SetValue(Convert.ToDouble(value, CultureInfo.InvariantCulture)); return; }
                string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                var combo = Editor as ComboBox;
                if (combo == null) { ((TextBox)Editor).Text = text; return; }
                if (combo.IsEditable) { combo.Text = text; return; }
                var item = combo.Items.Cast<ComboBoxItem>().FirstOrDefault(x => (string)x.Tag == text);
                if (item == null) { item = new ComboBoxItem { Content = text, Tag = text }; combo.Items.Add(item); }
                combo.SelectedItem = item;
            }
        }
        readonly ClockApp app;
        Settings baseline, draft;
        readonly List<FieldBinding> fields = new List<FieldBinding>();
        readonly List<FieldBinding> lineFields = new List<FieldBinding>();
        readonly CheckBox startup;
        readonly ComboBox lineSelector;
        readonly TextBlock status, previewCaption;
        readonly System.Windows.Shapes.Rectangle preview;
        readonly VisualBrush previewBrush;
        readonly DispatcherTimer previewTimer;
        bool loading = true;
        int lineIndex;

        public SettingsWindow(ClockApp owner)
        {
            app = owner; baseline = app.Clock.Config.Copy(); draft = baseline.Copy();
            Title = "轻时钟 · 自定义设置"; Width = 650; Height = 800; MinWidth = 560; MinHeight = 540;
            Icon = ClockApp.WindowIcon;
            MaxHeight = SystemParameters.WorkArea.Height - 32;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 13;
            Background = Brush("#F6F9FA"); Foreground = Brush("#233C46");
            var root = new DockPanel { Margin = new Thickness(24, 20, 24, 20) }; Content = root;
            var header = new Grid { Margin = new Thickness(0, 0, 0, 16) }; DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var intro = new StackPanel(); header.Children.Add(intro);
            var brand = new StackPanel { Orientation = Orientation.Horizontal };
            brand.Children.Add(new Image { Source = ClockApp.WindowIcon, Width = 38, Height = 38, Margin = new Thickness(0, 0, 9, 0) });
            brand.Children.Add(new TextBlock { Text = "轻时钟", FontSize = 28, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            intro.Children.Add(brand);
            intro.Children.Add(new TextBlock { Text = "内容、布局、外观和操作，都由你调整。", Foreground = Brush("#677C85"), Margin = new Thickness(0, 6, 0, 0) });
            // Mirror the actual clock visual so formatting, individual lines, clipping,
            // colors and timer updates cannot drift from what is on the desktop.
            previewBrush = new VisualBrush((Visual)app.Clock.Content)
            {
                AutoLayoutContent = false, ViewboxUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.Fill
            };
            preview = new System.Windows.Shapes.Rectangle { Fill = previewBrush, IsHitTestVisible = false };
            var previewBox = new Viewbox
            {
                Child = preview, MaxWidth = 168, MaxHeight = 72,
                Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var card = new StackPanel
            {
                Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "显示桌面时钟的实时内容与样式；尺寸较大时等比缩小。"
            };
            card.Children.Add(previewBox);
            previewCaption = new TextBlock
            {
                FontSize = 10.5, Foreground = Brush("#677C85"), TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 5, 0, 0)
            };
            card.Children.Add(previewCaption);
            Grid.SetColumn(card, 1); header.Children.Add(card);

            var footer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            status = new TextBlock { Text = "设置会实时预览到时钟；保存后保留。", TextWrapping = TextWrapping.Wrap, MinHeight = 36, Foreground = Brush("#677C85") }; footer.Children.Add(status);
            var buttonRow = new Grid(); buttonRow.ColumnDefinitions.Add(new ColumnDefinition()); buttonRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); footer.Children.Add(buttonRow);
            var files = new StackPanel { Orientation = Orientation.Horizontal }; buttonRow.Children.Add(files);
            files.Children.Add(MakeButton("导入", Import)); files.Children.Add(MakeButton("导出", Export));
            var actions = new StackPanel { Orientation = Orientation.Horizontal }; Grid.SetColumn(actions, 1); buttonRow.Children.Add(actions);
            actions.Children.Add(MakeButton("取消", delegate { Close(); }));
            actions.Children.Add(MakeButton("保存", delegate { Save(false); }));
            Button done = MakeButton("保存并关闭", delegate { Save(true); }); done.Background = Brush("#D1EAF2"); actions.Children.Add(done);

            var tabs = new TabControl { Background = Brushes.White, BorderBrush = Brush("#DCE7EB"), Padding = new Thickness(0) }; root.Children.Add(tabs);
            StackPanel content = Page(tabs, "内容");
            Section(content, "日期与时间格式");
            Hint(content, "一行格式对应一行显示，可以直接加入固定文字。默认沿用你在 ElevenClock 的设置。");
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
            monitors.AddRange(Forms.Screen.AllScreens.Select(s => s.DeviceName + "|" + s.DeviceName + "  " + s.Bounds.Width + " × " + s.Bounds.Height));
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
            string[] fonts = Fonts.SystemFontFamilies.Select(f => f.Source).Distinct().OrderBy(f => f).Select(f => f + "|" + f).ToArray();
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
            Section(behavior, "启动与配置");
            startup = new CheckBox { Content = "登录 Windows 时自动启动轻时钟", Margin = new Thickness(0, 6, 0, 8) }; behavior.Children.Add(startup);
            Hint(behavior, "开机启动在保存时生效，只管理轻时钟。若启用，请同时关闭 ElevenClock 的开机启动，避免重叠。");
            Button reset = MakeButton("恢复本次导入的默认样式", delegate { draft = new Settings(); LoadAll(); QueuePreview(null, null); });
            reset.HorizontalAlignment = HorizontalAlignment.Left; behavior.Children.Add(reset);
            Hint(behavior, "导入、导出包括全部外观和操作设置。配置保存在程序旁的 settings.json；保存时保留上一个版本作为备份。");

            previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            previewTimer.Tick += delegate { previewTimer.Stop(); Preview(); };
            app.Clock.SizeChanged += ClockSizeChanged;
            LoadAll(); startup.IsChecked = ClockApp.StartupEnabled(); loading = false; RefreshSample();
            Closed += delegate
            {
                previewTimer.Stop(); app.Clock.SizeChanged -= ClockSizeChanged;
                previewBrush.Visual = null; app.Clock.Apply(baseline);
            };
        }

        static SolidColorBrush Brush(string hex) { return new SolidColorBrush(Settings.ParseColor(hex)); }
        static Button MakeButton(string label, Action action)
        {
            var button = new Button { Content = label, Margin = new Thickness(0, 0, 7, 0), Padding = new Thickness(12, 7, 12, 7) };
            button.Click += delegate { action(); }; return button;
        }
        static StackPanel Page(TabControl tabs, string title)
        {
            var panel = new StackPanel { Margin = new Thickness(18, 6, 18, 16) };
            var item = new TabItem { Header = title, Padding = new Thickness(17, 9, 17, 9), Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } };
            tabs.Items.Add(item); return panel;
        }
        static void Section(StackPanel panel, string text)
        {
            panel.Children.Add(new TextBlock { Text = text, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 10) });
        }
        static void Hint(StackPanel panel, string text)
        {
            panel.Children.Add(new TextBlock { Text = text, FontSize = 11.5, Foreground = Brush("#677C85"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 10) });
        }
        FieldBinding Bind(string key, string title, FrameworkElement editor, bool line)
        {
            var binding = new FieldBinding { Key = key, Title = title, Editor = editor, Property = (line ? typeof(LineStyle) : typeof(Settings)).GetProperty(key) };
            System.Windows.Automation.AutomationProperties.SetName(editor, title + (line ? "（当前行）" : ""));
            var number = editor as NumberInput;
            if (number != null) number.SetLabel(title + (line ? "（当前行）" : ""));
            (line ? lineFields : fields).Add(binding); return binding;
        }
        void Row(StackPanel panel, string title, FrameworkElement editor, string hint)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(editor, 1); grid.Children.Add(editor); panel.Children.Add(grid);
            if (!String.IsNullOrEmpty(hint)) Hint(panel, hint);
        }
        void Field(StackPanel panel, string key, string title, string hint) { Field(panel, key, title, hint, false); }
        void Field(StackPanel panel, string key, string title, string hint, bool line)
        {
            var box = new NumberInput(NumberRules.For(key, line));
            Bind(key, title, box, line); box.ValueChanged += QueuePreview; Row(panel, title, box, hint);
        }
        void Multi(StackPanel panel, string key, string title, double height, bool line)
        {
            var box = new TextBox { AcceptsReturn = true, MinHeight = height, MaxHeight = height, Padding = new Thickness(8), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas") };
            Bind(key, title, box, line); box.TextChanged += QueuePreview; panel.Children.Add(box);
        }
        void Toggle(StackPanel panel, string key, string title, bool line)
        {
            var box = new CheckBox { Content = title, Margin = new Thickness(0, 5, 0, 8) };
            Bind(key, title, box, line); box.Click += QueuePreview; panel.Children.Add(box);
        }
        static ComboBox CreateComboBox(bool editable)
        {
            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(7, 4, 7, 4)));
            itemStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 28.0));
            var combo = new ComboBox
            {
                IsEditable = editable,
                MinHeight = 32,
                Padding = new Thickness(7, 0, 7, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                MaxDropDownHeight = 280,
                ItemContainerStyle = itemStyle
            };
            // Editable ComboBoxes contain a separate TextBox in the Windows theme template.
            // Center that editor as well as the closed selection and dropdown items.
            combo.Loaded += delegate
            {
                var editor = combo.Template.FindName("PART_EditableTextBox", combo) as TextBox;
                if (editor != null)
                {
                    editor.VerticalContentAlignment = VerticalAlignment.Center;
                    editor.VerticalAlignment = VerticalAlignment.Stretch;
                    editor.Padding = new Thickness(0);
                }
            };
            return combo;
        }
        void Choice(StackPanel panel, string key, string title, string[] choices, bool editable, bool line)
        {
            var combo = CreateComboBox(editable);
            foreach (string choice in choices)
            {
                int split = choice.IndexOf('|'); string id = split < 0 ? choice : choice.Substring(0, split);
                combo.Items.Add(new ComboBoxItem { Tag = id, Content = split < 0 ? choice : choice.Substring(split + 1) });
            }
            Bind(key, title, combo, line); combo.SelectionChanged += QueuePreview;
            if (editable) combo.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(QueuePreview));
            Row(panel, title, combo, null);
        }
        void ColorField(StackPanel panel, string key, string title, bool line)
        {
            var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var box = new TextBox { Padding = new Thickness(7, 5, 7, 5), MinHeight = 32 };
            Bind(key, title, box, line); box.TextChanged += QueuePreview; grid.Children.Add(box);
            Button button = MakeButton("选择…", delegate
            {
                using (var dialog = new Forms.ColorDialog())
                {
                    dialog.FullOpen = true;
                    try { dialog.Color = System.Drawing.ColorTranslator.FromHtml(box.Text); } catch { }
                    if (dialog.ShowDialog(new OwnerHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle)) == Forms.DialogResult.OK)
                        box.Text = "#" + dialog.Color.R.ToString("X2") + dialog.Color.G.ToString("X2") + dialog.Color.B.ToString("X2");
                }
            }); button.Margin = new Thickness(6, 0, 0, 0); Grid.SetColumn(button, 1); grid.Children.Add(button);
            Row(panel, title, grid, null);
        }
        sealed class OwnerHandle : Forms.IWin32Window
        {
            public IntPtr Handle { get; private set; }
            public OwnerHandle(IntPtr handle) { Handle = handle; }
        }
        void LoadAll()
        {
            bool wasLoading = loading; loading = true;
            foreach (var field in fields) field.Load(draft);
            foreach (var field in lineFields) field.Load(draft.Lines[lineIndex]);
            loading = wasLoading;
        }
        void SwitchLine(object sender, SelectionChangedEventArgs e)
        {
            if (loading || lineSelector.SelectedIndex < 0) return;
            try
            {
                LineStyle value = draft.Lines[lineIndex].Copy();
                foreach (var field in lineFields) field.Property.SetValue(value, field.Read(), null);
                value.Validate(); draft.Lines[lineIndex] = value; lineIndex = lineSelector.SelectedIndex;
                loading = true; foreach (var field in lineFields) field.Load(draft.Lines[lineIndex]); loading = false;
            }
            catch (Exception ex) { Error(ex.Message); loading = true; lineSelector.SelectedIndex = lineIndex; loading = false; }
        }
        Settings Collect()
        {
            Settings value = draft.Copy();
            foreach (var field in fields) field.Property.SetValue(value, field.Read(), null);
            foreach (var field in lineFields) field.Property.SetValue(value.Lines[lineIndex], field.Read(), null);
            value.Validate(); return value;
        }
        void QueuePreview(object sender, EventArgs e)
        {
            if (loading || previewTimer == null) return; previewTimer.Stop(); previewTimer.Start();
        }
        void Preview()
        {
            try
            {
                Settings value = Collect(); draft = value.Copy(); app.Clock.Apply(value); RefreshSample();
                int lineCount = ClockFormat.Render(value.Format, value.DisplayTime(DateTime.UtcNow), CultureInfo.GetCultureInfo(value.Culture)).Split('\n').Length;
                int enabledCount = value.Lines.Take(lineCount).Count(l => l.Enabled);
                double required = value.Lines.Take(lineCount).Where(l => l.Enabled).Sum(l => l.Custom ? l.Height : value.LineHeight);
                required += Math.Max(0, enabledCount - 1) * value.LineGap + value.PaddingTop + value.PaddingBottom + value.BorderWidth * 2;
                status.Text = value.Width < 56 || required > value.Height ? "当前尺寸可能裁切文字；可以增大宽高，或减小字号、行高及边距。" : "正在实时预览；保存后保留，取消会还原。";
                status.Foreground = Brush("#677C85");
            }
            catch (Exception ex) { Error(ex.Message); }
        }
        void ClockSizeChanged(object sender, SizeChangedEventArgs e) { RefreshSample(); }
        void RefreshSample()
        {
            double width = app.Clock.Width, height = app.Clock.Height;
            if (Double.IsNaN(width) || Double.IsNaN(height) || width <= 0 || height <= 0) return;
            preview.Width = width; preview.Height = height;
            // Explicit bounds preserve empty padding and the window's crop, even
            // when translated text extends outside its visible client area.
            previewBrush.Viewbox = new Rect(0, 0, width, height);
            preview.Opacity = app.Clock.Config.WindowOpacity / 100;
            double scale = Math.Min(1, Math.Min(168 / width, 72 / height));
            previewCaption.Text = "实时预览 · " + (scale * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
            System.Windows.Automation.AutomationProperties.SetName(preview,
                "时钟实时预览，" + width.ToString("0.#") + " × " + height.ToString("0.#"));
        }
        void Error(string message) { status.Text = message; status.Foreground = Brush("#AD442D"); }
        void Save(bool close)
        {
            try
            {
                Settings value = Collect(); Store.Save(value); baseline = value.Copy(); draft = value.Copy(); app.Clock.Apply(value); RefreshSample();
                ClockApp.SetStartup(startup.IsChecked == true);
                status.Text = "已保存。"; status.Foreground = Brush("#276749"); if (close) Close();
            }
            catch (Exception ex) { Store.Log(ex); Error("保存未完成：" + ex.Message); }
        }
        void Import()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "轻时钟配置 (*.json)|*.json", Title = "导入轻时钟配置" };
            if (dialog.ShowDialog(this) != true) return;
            try { draft = Store.Read(dialog.FileName); LoadAll(); Preview(); }
            catch (Exception ex) { Error("无法导入：" + ex.Message); }
        }
        void Export()
        {
            try
            {
                Settings value = Collect();
                var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "轻时钟配置 (*.json)|*.json", FileName = "LiteClock-theme.json", Title = "导出当前自定义配置" };
                if (dialog.ShowDialog(this) == true)
                {
                    File.WriteAllText(dialog.FileName, new JavaScriptSerializer().Serialize(value), new System.Text.UTF8Encoding(false));
                    status.Text = "已导出当前配置。"; status.Foreground = Brush("#276749");
                }
            }
            catch (Exception ex) { Error("无法导出：" + ex.Message); }
        }
    }
}
