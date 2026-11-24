// SPDX-License-Identifier: GPL-2.0-only
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AetherDrop;

static partial class Check
{
    static void ReceiveToggleResponsiveness(App app)
    {
        var python = Path.GetFullPath("build/python/python.exe");
        var start = new ProcessStartInfo(python) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import sys,time; sys.stdin.readline(); time.sleep(0.7)");
        using var child = Process.Start(start)!;
        var processField = typeof(Engine).GetField("_process", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var apply = typeof(App).GetMethod("ApplyDiscoverableAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var wasOn = app.Settings.Discoverable;
        try
        {
            processField.SetValue(app.Engine, child);
            app.Settings.Discoverable = false;
            var watch = Stopwatch.StartNew();
            var task = (Task)apply.Invoke(app, new object[] { Task.CompletedTask })!;
            Assert(watch.ElapsedMilliseconds < 150, "Receive shutdown returns immediately without waiting for the child process");
            Pump(50);
            Assert(!task.IsCompleted && !child.HasExited, "Dispatcher remains responsive while an engine delays its exit");
            app.Settings.Discoverable = true;
            var latest = (Task)apply.Invoke(app, new object[] { task })!;
            Pump(1000);
            Assert(task.IsCompletedSuccessfully && latest.IsCompletedSuccessfully && child.HasExited && app.Settings.Discoverable,
                "Rapid off/on waits for shutdown and preserves the latest receiving preference");
        }
        finally
        {
            if (!child.HasExited) child.Kill();
            processField.SetValue(app.Engine, null);
            app.Settings.Discoverable = wasOn;
        }
    }
}
