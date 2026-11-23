// SPDX-License-Identifier: GPL-2.0-only
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace NekoDrop;

/// <summary>Offline UI simulation. No engine, service, shell registration or real file writes.</summary>
public sealed class UiDebugSession : IDisposable
{
    readonly App _app;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    readonly Stopwatch _clock = new();
    readonly Forms.NotifyIcon _tray;
    string? _id;
    int _sequence;
    bool _started, _paused, _disposed;
    const double Total = 600_000_000;
    double _done, _lastTick, _nextChange, _simulatedRate;
    public event Action? Changed;
    public string Status { get; private set; } = "就绪：点击按钮模拟弹窗";
    public bool Paused => _paused;
    public string? ActiveId => _id;

    public UiDebugSession(App app)
    {
        if (!app.UiDebugMode) throw new InvalidOperationException("UI debug mode is required.");
        _app = app;
        _tray = new Forms.NotifyIcon { Text = "AetherDrop · UI 调试", Icon = Branding.TrayIcon(), Visible = true };
        _tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ShowTray(); };
        _timer.Tick += (_, _) => Tick();
    }
    void SetStatus(string message) { Status = message; Changed?.Invoke(); }
    void Send(JsonObject message) => _app.InjectUiDebugEvent(message);
    public void ShowTray() => _app.ShowUiDebugTray();
    public void Invite(bool autoAccept = false)
    {
        Reset();
        if (!_app.Settings.Discoverable) _app.SetDiscoverable(true);
        _id = "ui-debug-" + (++_sequence);
        Send(new JsonObject { ["event"] = "asked", ["id"] = _id, ["sender"] = "演示 iPhone", ["model"] = "iPhone15,2", ["waits"] = true,
            ["files"] = new JsonArray(new JsonObject { ["name"] = "IMG_0074.JPG", ["type"] = "image/jpeg", ["size"] = Total }) });
        SetStatus("等待选择接受或拒绝（模拟数据）");
        if (autoAccept) _app.Answer(_id, true);
        _timer.Start();
    }
    void Tick()
    {
        if (_id is null) return;
        var request = _app.Model.Requests.FirstOrDefault(x => x.Id == _id);
        if (request is null) { _timer.Stop(); _id = null; SetStatus("邀请已拒绝或关闭，可再次触发"); return; }
        if (!request.Answered) return;
        if (!_started)
        {
            _started = true; _clock.Restart();
            Send(new JsonObject { ["event"] = "incoming", ["id"] = _id, ["done"] = 0.0, ["total"] = Total });
        }
        if (_paused) return;
        double now = _clock.Elapsed.TotalSeconds;
        if (now >= _nextChange)
        {
            // Variable synthetic payloads for UI testing, never an asserted real network speed.
            _simulatedRate = (8 + Random.Shared.NextDouble() * 52) * 1_000_000;
            _nextChange = now + .4 + Random.Shared.NextDouble();
        }
        _done = Math.Min(Total, _done + Math.Max(0, now - _lastTick) * _simulatedRate);
        _lastTick = now;
        double done = _done;
        Send(new JsonObject { ["event"] = "incoming", ["id"] = _id, ["done"] = done, ["total"] = Total });
        SetStatus($"模拟接收 {done / Total:P0} · 动态模拟数据，非真实网速");
        if (done >= Total) Complete();
    }
    public void TogglePause()
    {
        if (_id is null || !_started) return;
        _paused = !_paused;
        if (_paused) _clock.Stop(); else _clock.Start();
        SetStatus(_paused ? "模拟传输已暂停" : "继续模拟传输");
    }
    public void SetProgress(double percent)
    {
        if (_id is null) return;
        _app.Answer(_id, true);
        _done = Total * Math.Clamp(percent / 100, 0, 1); _lastTick = _clock.Elapsed.TotalSeconds;
        Send(new JsonObject { ["event"] = "incoming", ["id"] = _id, ["done"] = _done, ["total"] = Total });
        SetStatus($"模拟进度 {percent:F0}%");
    }
    public void Complete()
    {
        if (_id is null) return;
        _timer.Stop();
        Send(new JsonObject { ["event"] = "stored", ["id"] = _id, ["folder"] = "模拟保存位置（不会写入文件）", ["files"] = new JsonArray("IMG_0074.JPG") });
        _id = null; SetStatus("模拟接收完成：没有创建实际文件");
    }
    public void Fail()
    {
        if (_id is null) return;
        _timer.Stop();
        Send(new JsonObject { ["event"] = "lost", ["id"] = _id, ["reason"] = "模拟连接中断" });
        _id = null; SetStatus("已模拟传输失败");
    }
    public void Expire()
    {
        if (_id is null) return;
        _timer.Stop();
        Send(new JsonObject { ["event"] = "answered", ["id"] = _id, ["how"] = "expired" });
        _id = null; SetStatus("已模拟邀请超时");
    }
    public void Reset()
    {
        _timer.Stop(); _clock.Reset(); _id = null; _started = _paused = false;
        _done = _lastTick = _nextChange = _simulatedRate = 0;
        Send(new JsonObject { ["event"] = "ready" });
        _app.Model.ReceivedFiles.Clear();
        SetStatus("已重置，可以重新触发弹窗");
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _timer.Stop(); _tray.Visible = false; _tray.Icon?.Dispose(); _tray.Dispose();
        Reset();
    }
}
