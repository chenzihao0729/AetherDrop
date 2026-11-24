// SPDX-License-Identifier: GPL-2.0-only
namespace AetherDrop.Service;

/// <summary>Per-connection activity leases; callers serialize access with the supervisor lock.</summary>
public sealed class TransferActivityLeases
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(20);
    readonly Dictionary<string, DateTime> _until = [];

    public void Update(string owner, bool active, DateTime now)
    {
        if (active) _until[owner] = now + Lifetime;
        else _until.Remove(owner);
    }

    public bool Any(DateTime now)
    {
        foreach (var owner in _until.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
            _until.Remove(owner);
        return _until.Count != 0;
    }
}
