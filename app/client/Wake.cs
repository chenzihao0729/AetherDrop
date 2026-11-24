// SPDX-License-Identifier: GPL-2.0-only
using Windows.Devices.Bluetooth.Advertisement;

namespace AetherDrop;

/// <summary>
/// On-demand mode: an iPhone that opens its AirDrop sheet advertises over
/// Bluetooth LE (Apple company 0x004C, type 0x05). Hearing that is the cue to
/// take the card, so this PC is listed by the time the sheet fills in.
/// </summary>
public sealed class Wake : IDisposable
{
    readonly BluetoothLEAdvertisementWatcher _watcher = new() { ScanningMode = BluetoothLEScanningMode.Passive };
    DateTime _last = DateTime.MinValue;
    public event Action? AirDropNearby;

    public bool Start()
    {
        _watcher.Received += (_, args) =>
        {
            foreach (var m in args.Advertisement.ManufacturerData)
            {
                if (m.CompanyId != 0x004C || m.Data.Length < 1) continue;
                var reader = Windows.Storage.Streams.DataReader.FromBuffer(m.Data);
                if (reader.ReadByte() != 0x05) continue;
                if (DateTime.UtcNow - _last < TimeSpan.FromSeconds(15)) return;
                _last = DateTime.UtcNow;
                AirDropNearby?.Invoke();
                return;
            }
        };
        try { _watcher.Start(); return true; }
        catch (Exception) { return false; /* no Bluetooth */ }
    }

    public void Dispose()
    {
        try { _watcher.Stop(); } catch (Exception) { }
    }
}
