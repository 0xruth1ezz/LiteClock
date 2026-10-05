using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LiteClock
{
    // Numeric constraints match Settings/LineStyle. Step controls the spinner;
    // manually entered decimal values retain the precision supported by the model.
    public sealed class NumberRules
    {
        public readonly decimal Minimum, Maximum, Step;
        public readonly bool IntegerOnly;
        static readonly Regex Complete = new Regex(@"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant);
        static readonly Regex Partial = new Regex(@"^[+-]?(?:[0-9]*(?:\.[0-9]*)?)(?:[eE][+-]?[0-9]*)?$", RegexOptions.CultureInvariant);

        public NumberRules(decimal minimum, decimal maximum, decimal step, bool integerOnly)
        {
            if (maximum < minimum || step <= 0) throw new ArgumentOutOfRangeException("step");
            Minimum = minimum; Maximum = maximum; Step = step; IntegerOnly = integerOnly;
        }
        public static NumberRules For(string key, bool line)
        {
            switch (key)
            {
                case "Width": return new NumberRules(24, 1200, 1, false);
                case "Height": return line ? new NumberRules(8, 160, 0.5m, false) : new NumberRules(24, 600, 1, false);
                case "Right": case "Bottom": return new NumberRules(0, 20000, 1, false);
                case "FontPoints": return new NumberRules(5, 72, 0.5m, false);
                case "LineHeight": return new NumberRules(8, 120, 0.5m, false);
                case "BackgroundOpacity": case "BorderOpacity": case "TextOpacity": return new NumberRules(0, 100, 1, false);
                case "WindowOpacity": return new NumberRules(5, 100, 1, false);
                case "PaddingLeft": case "PaddingRight": case "PaddingTop": case "PaddingBottom": case "Radius": return new NumberRules(0, 300, 1, false);
                case "OffsetX": case "OffsetY": return new NumberRules(-500, 500, 1, false);
                case "LineGap": return new NumberRules(0, 100, 0.5m, false);
                case "BorderWidth": return new NumberRules(0, 30, 0.5m, false);
                case "RefreshMilliseconds": return new NumberRules(100, 60000, 50, true);
                case "TimeOffsetMinutes": return new NumberRules(-10080, 10080, 0.1m, false);
                default: throw new ArgumentException("Missing numeric constraints: " + key);
            }
        }
        public static bool IsEditableText(string text)
        {
            if (text == null || text.Length > 64 || !Partial.IsMatch(text)) return false;
            int exponent = text.IndexOfAny(new[] { 'e', 'E' });
            return exponent < 0 || Regex.IsMatch(text.Substring(0, exponent), "[0-9]");
        }
        public static bool TryNumber(string text, out double value)
        {
            value = 0;
            text = (text ?? "").Trim();
            return Complete.IsMatch(text) && Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !Double.IsNaN(value) && !Double.IsInfinity(value);
        }
        public bool TryValue(string text, out double value, out string error)
        {
            error = null;
            if (!TryNumber(text, out value))
                error = String.IsNullOrWhiteSpace(text) ? "请填写数值。" : "请填写有效数字，小数用英文句点。";
            else if (value < (double)Minimum || value > (double)Maximum)
                error = "应在 " + Format(Minimum) + "–" + Format(Maximum) + " 之间。";
            else if (IntegerOnly && value != Math.Truncate(value)) error = "请填写整数。";
            return error == null;
        }
        public double Read(string text, string title)
        {
            double value; string error;
            if (!TryValue(text, out value, out error)) throw new ArgumentException(title + "：" + error);
            return value;
        }
        public bool CanStep(string text, int direction)
        {
            double value;
            return !TryNumber(text, out value) || (direction > 0 ? value < (double)Maximum : value > (double)Minimum);
        }
        public string SteppedText(string text, int direction)
        {
            if (direction != -1 && direction != 1) throw new ArgumentOutOfRangeException("direction");
            double parsed;
            bool valid = TryNumber(text, out parsed);
            if (valid && !CanStep(text, direction)) return text;
            if (!valid) parsed = 0;
            if (parsed < (double)Minimum) return Format(Minimum);
            if (parsed > (double)Maximum) return Format(Maximum);
            decimal current = (decimal)parsed;
            decimal position = (current - Minimum) / Step;
            decimal next = Minimum + (direction > 0 ? Decimal.Floor(position) + 1 : Decimal.Ceiling(position) - 1) * Step;
            return Format(Math.Max(Minimum, Math.Min(Maximum, next)));
        }
        static string Format(decimal value) { return value.ToString("0.############################", CultureInfo.InvariantCulture); }
    }

    public sealed class NumberInput : UserControl
    {
        readonly TextBox editor;
        readonly RepeatButton increase, decrease;
        readonly Border frame;
        readonly Brush normalBorder = new SolidColorBrush(Color.FromRgb(173, 190, 198));
        readonly Brush focusBorder = new SolidColorBrush(Color.FromRgb(56, 143, 175));
        readonly Brush errorBorder = new SolidColorBrush(Color.FromRgb(183, 67, 45));
        string label = "数值";
        int wheelRemainder;
        public NumberRules Rules { get; private set; }
        public event EventHandler ValueChanged;
        public string Text
        {
            get { return editor.Text; }
            set { editor.Text = value ?? ""; }
        }

        public NumberInput(NumberRules rules)
        {
            Rules = rules; MinHeight = 32; Focusable = false; IsTabStop = false;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
            frame = new Border { BorderBrush = normalBorder, BorderThickness = new Thickness(1), Background = Brushes.White, Child = grid };
            Content = frame;
            editor = new TextBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                Padding = new Thickness(7, 0, 5, 0), VerticalContentAlignment = VerticalAlignment.Center,
                AcceptsReturn = false, AcceptsTab = false, MaxLength = 64, MinWidth = 30 };
            InputMethod.SetIsInputMethodEnabled(editor, false);
            grid.Children.Add(editor);
            var arrows = new Grid(); arrows.RowDefinitions.Add(new RowDefinition()); arrows.RowDefinitions.Add(new RowDefinition());
            var arrowFrame = new Border { BorderBrush = normalBorder, BorderThickness = new Thickness(1, 0, 0, 0), Child = arrows };
            Grid.SetColumn(arrowFrame, 1); grid.Children.Add(arrowFrame);
            increase = MakeButton(true); decrease = MakeButton(false);
            Grid.SetRow(decrease, 1); arrows.Children.Add(increase); arrows.Children.Add(decrease);
            increase.Click += delegate { Spin(1); }; decrease.Click += delegate { Spin(-1); };
            editor.TextChanged += delegate
            {
                UpdateState();
                var changed = ValueChanged; if (changed != null) changed(this, EventArgs.Empty);
            };
            editor.PreviewTextInput += delegate(object sender, TextCompositionEventArgs e) { e.Handled = !NumberRules.IsEditableText(Proposed(e.Text)); };
            DataObject.AddPastingHandler(editor, OnPaste);
            editor.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (Keyboard.Modifiers != ModifierKeys.None) return;
                if (e.Key == Key.Up || e.Key == Key.Down) { Spin(e.Key == Key.Up ? 1 : -1); e.Handled = true; }
                else if (e.Key == Key.Enter) { NormalizeValidText(); e.Handled = true; }
            };
            editor.LostKeyboardFocus += delegate { wheelRemainder = 0; NormalizeValidText(); UpdateState(); };
            editor.GotKeyboardFocus += delegate { UpdateState(); };
            PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs e)
            {
                if (!editor.IsKeyboardFocusWithin || Keyboard.Modifiers != ModifierKeys.None) return;
                wheelRemainder += e.Delta;
                while (Math.Abs(wheelRemainder) >= 120)
                {
                    int direction = Math.Sign(wheelRemainder); Spin(direction); wheelRemainder -= direction * 120;
                }
                e.Handled = true;
            };
            SetLabel(label); UpdateState();
        }

        static RepeatButton MakeButton(bool up)
        {
            var arrow = new Path { Data = Geometry.Parse(up ? "M 0,4 L 4,0 8,4 Z" : "M 0,0 L 4,4 8,0 Z"),
                Fill = new SolidColorBrush(Color.FromRgb(55, 86, 98)), Width = 8, Height = 4, Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var button = new RepeatButton { Content = arrow, Focusable = false, IsTabStop = false, Delay = 350, Interval = 70,
                Background = new SolidColorBrush(Color.FromRgb(245, 249, 251)), BorderThickness = new Thickness(0), Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            button.Template = new ControlTemplate(typeof(RepeatButton)) { VisualTree = border };
            var style = new Style(typeof(RepeatButton));
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(209, 234, 242)))); style.Triggers.Add(hover);
            var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(178, 215, 228)))); style.Triggers.Add(pressed);
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.35)); style.Triggers.Add(disabled);
            // Background comes from the style so hover/pressed triggers can override it.
            button.ClearValue(Control.BackgroundProperty);
            style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(245, 249, 251))));
            button.Style = style; return button;
        }
        public void SetLabel(string text)
        {
            label = text;
            string help = "范围 " + Rules.Minimum + "–" + Rules.Maximum + "；按钮 / ↑↓ 每次 " + Rules.Step + "；聚焦后可用滚轮。";
            AutomationProperties.SetName(this, text); AutomationProperties.SetName(editor, text);
            AutomationProperties.SetHelpText(editor, help);
            AutomationProperties.SetName(increase, "增加" + text); AutomationProperties.SetName(decrease, "减少" + text);
            increase.ToolTip = "增加 " + Rules.Step + "（可长按）"; decrease.ToolTip = "减少 " + Rules.Step + "（可长按）";
            ToolTip = help;
        }
        public void SetValue(double value) { Text = value.ToString("G15", CultureInfo.InvariantCulture); }
        public double ReadValue() { return Rules.Read(Text, label); }
        public void Spin(int direction)
        {
            if (!IsEnabled || !Rules.CanStep(Text, direction)) return;
            editor.Focus();
            Text = Rules.SteppedText(Text, direction);
            editor.CaretIndex = editor.Text.Length;
        }
        string Proposed(string inserted)
        {
            return editor.Text.Remove(editor.SelectionStart, editor.SelectionLength).Insert(editor.SelectionStart, inserted);
        }
        void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText, true)) { e.CancelCommand(); return; }
            string pasted = ((string)e.DataObject.GetData(DataFormats.UnicodeText, true)).Trim();
            double number;
            if (!NumberRules.IsEditableText(Proposed(pasted)) || !NumberRules.TryNumber(Proposed(pasted), out number))
            { e.CancelCommand(); return; }
            e.DataObject = new DataObject(DataFormats.UnicodeText, pasted);
        }
        void NormalizeValidText()
        {
            double value; string error;
            if (Rules.TryValue(Text, out value, out error))
            {
                string normalized = value.ToString("G15", CultureInfo.InvariantCulture);
                if (Text != normalized) Text = normalized;
            }
        }
        void UpdateState()
        {
            double value; string error;
            bool valid = Rules.TryValue(Text, out value, out error);
            frame.BorderBrush = !valid ? errorBorder : editor.IsKeyboardFocusWithin ? focusBorder : normalBorder;
            AutomationProperties.SetHelpText(editor, valid ? "范围 " + Rules.Minimum + "–" + Rules.Maximum + "；步长 " + Rules.Step : label + "：" + error);
            increase.IsEnabled = Rules.CanStep(Text, 1); decrease.IsEnabled = Rules.CanStep(Text, -1);
        }
        protected override AutomationPeer OnCreateAutomationPeer() { return new NumberInputPeer(this); }

        sealed class NumberInputPeer : FrameworkElementAutomationPeer, IRangeValueProvider
        {
            NumberInput Input { get { return (NumberInput)Owner; } }
            public NumberInputPeer(NumberInput owner) : base(owner) { }
            protected override string GetClassNameCore() { return "NumberInput"; }
            protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.Spinner; }
            public override object GetPattern(PatternInterface pattern) { return pattern == PatternInterface.RangeValue ? this : base.GetPattern(pattern); }
            public bool IsReadOnly { get { return !Input.IsEnabled; } }
            public double Minimum { get { return (double)Input.Rules.Minimum; } }
            public double Maximum { get { return (double)Input.Rules.Maximum; } }
            public double SmallChange { get { return (double)Input.Rules.Step; } }
            public double LargeChange { get { return (double)Input.Rules.Step * 10; } }
            public double Value { get { double value; return NumberRules.TryNumber(Input.Text, out value) ? value : Double.NaN; } }
            public void SetValue(double value)
            {
                if (IsReadOnly) throw new ElementNotEnabledException();
                string text = value.ToString("G15", CultureInfo.InvariantCulture);
                Input.Rules.Read(text, Input.label); Input.SetValue(value);
            }
        }
    }
}
