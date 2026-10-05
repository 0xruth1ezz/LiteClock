using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
namespace LiteClock
{
    static class SelfTests
    {
        public static int Run(string[] args)
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
