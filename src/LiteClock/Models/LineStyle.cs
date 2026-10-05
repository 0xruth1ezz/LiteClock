using System;

namespace LiteClock;

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
