using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Globalization.NumberFormatting;

namespace LiteClock;

// The platform NumberBox supplies keyboard, wheel, repeat buttons and accessibility.
// Disabled native validation keeps incomplete drafts visible; the model validates save.
public sealed class NumberInput : UserControl
{
    public NumberRules Rules { get; }
    public NumberBox Editor { get; }
    public event EventHandler ValueChanged;
    string label = "数值";
    TextBox input;
    public string Text
    {
        get => input?.Text ?? Editor.Text;
        set { Editor.Text = value; if (input != null) input.Text = value; }
    }

    public NumberInput(NumberRules rules)
    {
        Rules = rules;
        Editor = new NumberBox
        {
            SmallChange = (double)rules.Step,
            LargeChange = (double)rules.Step * 10,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            ValidationMode = NumberBoxValidationMode.Disabled,
            Minimum = (double)rules.Minimum,
            Maximum = (double)rules.Maximum,
            IsWrapEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            NumberFormatter = new DecimalFormatter(new[] { "en-US" }, "US")
            {
                IntegerDigits = 1, FractionDigits = 0,
                IsGrouped = false,
                NumberRounder = new SignificantDigitsNumberRounder { SignificantDigits = 15 }
            }
        };
        // NumberBox.Text only commits on Enter/focus loss. Observe the native
        // text editor too, so preview/save see even unfinished or invalid drafts.
        Editor.Loaded += (_, _) => ConnectEditor();
        Editor.ValueChanged += (_, e) =>
        {
            if (input == null || double.IsNaN(e.OldValue) || double.IsNaN(e.NewValue)) return;
            double change = e.NewValue - e.OldValue;
            bool step = Math.Abs(Math.Abs(change) - Editor.SmallChange) < 0.00000001
                || Math.Abs(Math.Abs(change) - Editor.LargeChange) < 0.00000001;
            // Native stepping raises ValueChanged before formatting the new text.
            // Typed commits already contain their new value in the TextBox.
            if (!step || (NumberRules.TryNumber(input.Text, out double typed) && Math.Abs(typed - e.OldValue) > 0.00000001)) return;
            string next = input.Text;
            int steps = Math.Abs(change) > Editor.SmallChange + 0.00000001 ? 10 : 1;
            for (int i = 0; i < steps; i++) next = Rules.SteppedText(next, Math.Sign(change));
            if (NumberRules.TryNumber(next, out double bounded)) Editor.Value = bounded;
        };
        Editor.RegisterPropertyChangedCallback(NumberBox.TextProperty, (_, _) =>
        {
            if (input == null) ValueChanged?.Invoke(this, EventArgs.Empty);
        });
        Content = Editor;
    }
    internal void ConnectEditor()
    {
        Editor.ApplyTemplate();
        var found = FindInput(Editor);
        if (found == input || found == null) return;
        if (input != null) input.TextChanged -= TextChanged;
        input = found; input.TextChanged += TextChanged;
    }
    static TextBox FindInput(DependencyObject parent)
    {
        if (parent is TextBox box && box.Name == "InputBox") return box;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var found = FindInput(VisualTreeHelper.GetChild(parent, i));
            if (found != null) return found;
        }
        return null;
    }
    void TextChanged(object sender, TextChangedEventArgs args)
    {
        bool valid = Rules.TryValue(Text, out _, out string error);
        if (valid) Editor.ClearValue(Control.BorderBrushProperty);
        else Editor.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.IndianRed);
        AutomationProperties.SetItemStatus(Editor, error ?? "");
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }
    public void SetLabel(string value)
    {
        label = value;
        AutomationProperties.SetName(Editor, value);
        AutomationProperties.SetHelpText(Editor, $"范围 {Rules.Minimum}–{Rules.Maximum}；步长 {Rules.Step}");
    }
    public void SetValue(double value) => Text = value.ToString("G15", CultureInfo.InvariantCulture);
    public double ReadValue() => Rules.Read(Text, label);
    public void Spin(int direction) => Text = Rules.SteppedText(Text, direction);
}
