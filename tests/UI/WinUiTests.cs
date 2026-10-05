using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using LiteClock;

static class WinUiTests
{
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        yield return parent;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(parent, i))) yield return child;
    }
    static void Check(bool value, string label, string report)
    {
        if (!value) throw new Exception("FAIL: " + label);
        File.AppendAllText(report, "PASS: " + label + "\n");
    }
    public static async Task Run(ClockApp app, string report)
    {
        var collected = app.Editor.Collect();
        Check(collected.Format == app.Clock.Config.Format && collected.Format.Split('\n').Length == 3,
            "WinUI multiline TextBox preserves LF format on collect", report);
        var navigation = Descendants(app.Editor.Content).OfType<NavigationView>().Single();
        Check(navigation.MenuItems.Count == 6, "all six settings pages including app information use NavigationView", report);
        foreach (var page in navigation.MenuItems) { navigation.SelectedItem = page; await Task.Delay(60); }
        Check(app.Editor.Collect().FontPoints == collected.FontPoints, "loading every settings page retains values", report);
        var input = new NumberInput(NumberRules.For("FontPoints", false));
        var host = new Window { Content = input, Title = "轻时钟 · 控件测试" };
        Native.Center(host, 300, 140); input.SetLabel("测试字号"); input.SetValue(8); host.Activate();
        try
        {
            await Task.Delay(150); input.ConnectEditor();
            var text = Descendants(input).OfType<TextBox>().Single(x => x.Name == "InputBox");
            var up = Descendants(input).OfType<FrameworkElement>().Single(x => x.Name == "UpSpinButton");
            var down = Descendants(input).OfType<FrameworkElement>().Single(x => x.Name == "DownSpinButton");
            async Task Invoke(FrameworkElement element)
            {
                var peer = FrameworkElementAutomationPeer.CreatePeerForElement(element);
                ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
                await Task.Delay(70);
            }
            int changes = 0; input.ValueChanged += (_, _) => changes++;
            text.Text = "8.25"; await Task.Delay(70);
            Check(input.ReadValue() == 8.25 && changes > 0, "uncommitted NumberBox text updates preview and retains precision", report);
            text.Text = "-"; await Task.Delay(70);
            bool rejected = false; try { input.ReadValue(); } catch (ArgumentException) { rejected = true; }
            Check(rejected && input.Text == "-", "native incomplete draft is rejected without overwrite", report);
            input.SetValue(8); await Invoke(up);
            Check(input.ReadValue() == 8.5, "native NumberBox repeat button applies half-point step", report);
            input.SetValue(72); await Invoke(up);
            Check(input.ReadValue() == 72, "native NumberBox spinner respects upper bound", report);
            input.SetValue(5); await Invoke(down);
            Check(input.ReadValue() == 5, "native NumberBox spinner respects lower bound", report);
            input.SetValue(8.25); await Invoke(up);
            Check(input.ReadValue() == 8.5, "native step snaps to model grid", report);
            text.Text = "100"; await Task.Delay(70);
            rejected = false; try { input.ReadValue(); } catch (ArgumentException) { rejected = true; }
            Check(rejected && input.Text == "100", "out-of-range typed draft is retained and cannot be saved", report);
        }
        finally { host.Close(); }
    }
}
