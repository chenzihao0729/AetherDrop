using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AetherDrop;

internal static class Check
{
    [STAThread]
    static void Main()
    {
        var app = new App(uiDebugMode: true) { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(Theme.Build());
        L.Use("zh");
        var card = new ReceiveWindow(new Request { Id = "rate-test", Sender = "iPhone", Model = "iPhone15,2", What = "" }, (_, _) => { });
        T Field<T>(string name) => (T)typeof(ReceiveWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(card)!;
        var timer = Field<DispatcherTimer>("_speedTimer");
        var label = Field<TextBlock>("_rate");
        var title = Field<TextBlock>("_heading");
        Assert(timer.Interval == TimeSpan.FromMilliseconds(800), "rate timer preserves 0.8-second refresh");
        card.Update(0, 10_000_000);
        card.Update(8_000_000, 10_000_000);
        Assert(title.Text.Contains("80%"), "percentage updates immediately without waiting for the rate timer");
        Pump(400);
        Assert(label.Text == "正在计算…", "rate is not refreshed before the requested interval");
        Pump(500);
        Assert(label.Text.Contains("MB/s"), "received byte deltas produce a measured rate");
        Pump(850);
        Assert(label.Text == "0 KB/s", "stalled data produces zero rate on the next sample");
        card.Finish(true, "", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Assert(!timer.IsEnabled, "completion stops rate sampling immediately");
        card.Close();
        app.Shutdown();
    }

    static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
}
