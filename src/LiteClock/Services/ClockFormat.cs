using System;
using System.Globalization;
using System.Text;

namespace LiteClock;

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
