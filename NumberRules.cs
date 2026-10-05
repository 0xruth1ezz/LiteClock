using System;
using System.Globalization;
using System.Text.RegularExpressions;
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

}
