using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LiteClock;

static class NumberInputTests
{
    [STAThread]
    static int Main(string[] args)
    {
        var results = new List<string>();
        Action<bool, string> check = delegate(bool passed, string name) { if (!passed) throw new Exception("FAIL: " + name); results.Add("PASS: " + name); };
        try
        {
            foreach (var type in new[] { typeof(Settings), typeof(LineStyle) })
                foreach (var field in type.GetProperties().Where(p => p.PropertyType == typeof(double) || p.PropertyType == typeof(int)))
                    NumberRules.For(field.Name, type == typeof(LineStyle));
            check(true, "every numeric model field has min/max/step rules");
            double value; string error;
            var width = NumberRules.For("Width", false);
            check(width.SteppedText("60", 1) == "61" && width.SteppedText("60", -1) == "59", "normal increment and decrement");
            check(width.SteppedText("1200", 1) == "1200" && width.SteppedText("24", -1) == "24", "spinner cannot cross bounds");
            check(width.SteppedText("5000", -1) == "1200" && width.SteppedText("10", 1) == "24", "stepping repairs out-of-range input in the correct direction");
            check(width.SteppedText("5000", 1) == "5000" && width.SteppedText("10", -1) == "10", "stepping never reverses the requested direction");
            check(width.SteppedText("", 1) == "24" && !width.TryValue("", out value, out error), "empty editing state is allowed but cannot be saved");
            check(width.TryValue("60.25", out value, out error) && value == 60.25, "manual decimal precision is preserved");
            var offset = NumberRules.For("OffsetX", false);
            check(offset.TryValue("-1.25", out value, out error) && value == -1.25 && offset.SteppedText("-1.25", 1) == "-1" && offset.SteppedText("-1.25", -1) == "-2", "negative values and off-step rounding");
            var font = NumberRules.For("FontPoints", false);
            check(font.SteppedText("8", 1) == "8.5" && font.SteppedText("8.5", -1) == "8", "half-point font changes");
            var minutes = NumberRules.For("TimeOffsetMinutes", false);
            string stepped = "0";
            for (int i = 0; i < 100; i++) stepped = minutes.SteppedText(stepped, 1);
            check(stepped == "10", "decimal stepping avoids floating-point drift");
            var refresh = NumberRules.For("RefreshMilliseconds", false);
            check(refresh.SteppedText("250", 1) == "300" && !refresh.TryValue("250.5", out value, out error) && refresh.TryValue("251", out value, out error), "refresh allows integer entry and 50 ms stepping");
            check(NumberRules.IsEditableText("-") && NumberRules.IsEditableText(".") && NumberRules.IsEditableText("1e-") && !NumberRules.IsEditableText("abc") && !NumberRules.IsEditableText("1..2") && !NumberRules.IsEditableText("e"), "typing supports intermediate numeric states and blocks nonnumbers");
            check(NumberRules.TryNumber("1.5e2", out value) && value == 150 && !NumberRules.TryNumber("NaN", out value) && !NumberRules.TryNumber("Infinity", out value) && !NumberRules.TryNumber("1e999", out value), "scientific notation and non-finite rejection");
            check(!width.TryValue("23.99", out value, out error) && !width.TryValue("1200.01", out value, out error), "typed min and max validation");
            var input = new NumberInput(font); input.SetLabel("字号"); int changes = 0;
            input.ValueChanged += delegate { changes++; };
            input.SetValue(8); input.Spin(1);
            check(input.ReadValue() == 8.5 && changes == 2, "control loading and spin notify preview");
            input.Text = "-"; bool invalid = false;
            try { input.ReadValue(); } catch (ArgumentException) { invalid = true; }
            check(invalid && input.Text == "-", "invalid drafts are retained without becoming persisted values");
            results.Add("ALL NUMBER INPUT TESTS PASSED");
            File.WriteAllLines(args[0], results); return 0;
        }
        catch (Exception ex) { results.Add(ex.ToString()); File.WriteAllLines(args[0], results); return 1; }
    }
}
