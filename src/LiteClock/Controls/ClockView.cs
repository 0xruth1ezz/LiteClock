using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI.Text;
using FontWeights = Microsoft.UI.Text.FontWeights;

namespace LiteClock;

// One WinUI visual implementation is shared by the desktop and preview windows.
public sealed class ClockView : Grid
{
    readonly Border surface;
    readonly StackPanel lines;
    readonly List<TextBlock> labels = new();
    Settings config;
    bool hover;
    string lastText;
    public string DisplayText => lastText ?? "";
    public ClockView()
    {
        Background = new SolidColorBrush(Colors.Transparent);
        lines = new StackPanel();
        for (int i = 0; i < 8; i++)
        {
            var label = new TextBlock { TextWrapping = TextWrapping.NoWrap, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, IsTextSelectionEnabled = false };
            labels.Add(label); lines.Children.Add(label);
        }
        surface = new Border { Child = lines };
        Children.Add(surface);
        SizeChanged += (_, e) => Clip = new RectangleGeometry { Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
        PointerEntered += (_, _) => { hover = true; UpdateColors(); };
        PointerExited += (_, _) => { hover = false; UpdateColors(); };
    }
    public void Apply(Settings value)
    {
        config = value.Copy();
        Opacity = config.WindowOpacity / 100;
        lines.VerticalAlignment = Enum.Parse<VerticalAlignment>(config.Vertical);
        lines.RenderTransform = new TranslateTransform { X = config.OffsetX, Y = config.OffsetY };
        lines.Opacity = config.TextOpacity / 100;
        surface.Padding = new Thickness(config.PaddingLeft, config.PaddingTop, config.PaddingRight, config.PaddingBottom);
        surface.CornerRadius = new CornerRadius(config.Radius);
        surface.BorderThickness = new Thickness(config.BorderWidth);
        surface.BorderBrush = ColorBrush(config.BorderColor, config.BorderOpacity);
        ToolTipService.SetToolTip(this, config.TooltipEnabled ? config.Tooltip : null);
        for (int i = 0; i < labels.Count; i++)
        {
            var style = config.Lines[i]; var label = labels[i];
            label.FontFamily = new FontFamily(style.Custom ? style.FontFamily : config.FontFamily);
            label.FontSize = (style.Custom ? style.FontPoints : config.FontPoints) * 96 / 72;
            label.FontWeight = (style.Custom ? style.Bold : config.Bold) ? FontWeights.Bold : FontWeights.Normal;
            label.FontStyle = (style.Custom ? style.Italic : config.Italic) ? FontStyle.Italic : FontStyle.Normal;
            label.TextDecorations = (style.Custom ? style.Underline : config.Underline) ? TextDecorations.Underline : TextDecorations.None;
            label.TextAlignment = Enum.Parse<TextAlignment>(style.Custom ? style.Alignment : config.Alignment);
            label.Height = style.Custom ? style.Height : config.LineHeight;
            label.LineHeight = label.Height;
            label.RenderTransform = new TranslateTransform { X = style.Custom ? style.OffsetX : 0, Y = style.Custom ? style.OffsetY : 0 };
        }
        UpdateColors(); lastText = null; UpdateTime(DateTime.UtcNow);
    }
    static SolidColorBrush ColorBrush(string hex, double opacity)
    {
        var color = Settings.ParseColor(hex); color.A = (byte)Math.Round(opacity * 255 / 100);
        return new SolidColorBrush(color);
    }
    void UpdateColors()
    {
        if (config == null) return;
        bool over = hover && config.HoverEnabled;
        surface.Background = ColorBrush(over ? config.HoverBackground : config.Background, config.BackgroundOpacity);
        for (int i = 0; i < labels.Count; i++)
            labels[i].Foreground = ColorBrush(over ? config.HoverForeground : config.Lines[i].Custom ? config.Lines[i].Color : config.Foreground, 100);
    }
    public void UpdateTime(DateTime utc)
    {
        if (config == null) return;
        string text = ClockFormat.Render(config.Format, config.DisplayTime(utc), CultureInfo.GetCultureInfo(config.Culture));
        if (text == lastText) return;
        string[] rendered = text.Split('\n'); int last = -1;
        for (int i = 0; i < labels.Count; i++)
        {
            bool shown = i < rendered.Length && config.Lines[i].Enabled;
            labels[i].Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            labels[i].Text = i < rendered.Length ? rendered[i] : "";
            labels[i].Margin = new Thickness(0, 0, 0, config.LineGap);
            if (shown) last = i;
        }
        if (last >= 0) labels[last].Margin = new Thickness(0);
        lastText = text;
    }
}
