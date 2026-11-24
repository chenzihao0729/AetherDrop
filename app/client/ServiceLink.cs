// SPDX-License-Identifier: GPL-2.0-only
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;

namespace AetherDrop;

/// <summary>The connection to AetherDropService (\\.\pipe\AetherDrop), kept up for the app's lifetime.</summary>
public sealed class ServiceLink : IDisposable
{
    readonly CancellationTokenSource _cts = new();
    StreamWriter? _writer;

    /// <summary>Raised on the thread pool for every "state" event.</summary>
    public event Action<JsonObject>? State;
    public event Action<bool>? Connected;
    /// <summary>Answers to commands (e.g. wifi-connect) and other non-state events.</summary>
    public event Action<JsonObject>? Reply;
    public JsonObject? Last { get; private set; }

    public void Start() => _ = Task.Run(Loop);

    async Task Loop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", "AetherDrop", PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(3000, _cts.Token);
                var reader = new StreamReader(pipe, Encoding.UTF8);
                _writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
                Connected?.Invoke(true);
                while (!_cts.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(_cts.Token);
                    if (line is null) break;
                    if (JsonNode.Parse(line) is not JsonObject o) continue;
                    if ((string?)o["event"] == "state")
                    {
                        Last = o;
                        State?.Invoke(o);
                    }
                    else Reply?.Invoke(o);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) { }
            _writer = null;
            Connected?.Invoke(false);
            try { await Task.Delay(2000, _cts.Token); } catch (OperationCanceledException) { break; }
        }
    }

    public void Send(JsonObject command)
    {
        var w = _writer;
        if (w is null) return;
        try { w.WriteLine(command.ToJsonString()); }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    public void Wake() => Send(new JsonObject { ["do"] = "wake" });
    public void Touch() => Send(new JsonObject { ["do"] = "touch" });
    public void TransferActivity(bool active) => Send(new JsonObject { ["do"] = "transfer-activity", ["active"] = active });
    public void SetMode(string mode) => Send(new JsonObject { ["do"] = "mode", ["mode"] = mode });
    public void SetFastUnlock(bool on) => Send(new JsonObject { ["do"] = "fast-unlock", ["on"] = on });
    public void SetCoexPlan(bool on) => Send(new JsonObject { ["do"] = "coex-plan", ["on"] = on });
    /// <summary>Asks for the card list and models (answered with a "cards" event).</summary>
    public void RequestCards() => Send(new JsonObject { ["do"] = "cards" });
    /// <summary>Runs `hardwareId` as `model`; "" and "" go back to automatic.</summary>
    public void SetManualCard(string hardwareId, string model) =>
        Send(new JsonObject { ["do"] = "card-manual", ["hw"] = hardwareId, ["model"] = model });

    public void Dispose() => _cts.Cancel();
}
