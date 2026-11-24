// SPDX-License-Identifier: GPL-2.0-only
using System.Net;
using System.Runtime.InteropServices;

namespace AetherDrop.Service;

/// <summary>
/// An ICMP echo sent from a given source address (IcmpSendEcho2Ex), so the
/// gateway probe goes out on the "AetherDrop" adapter and not on whatever
/// interface Windows would route by (Codex review round 2 #15: Ethernet or a
/// VPN could answer while the Wi-Fi bridge is broken).
/// </summary>
static class Icmp
{
    [DllImport("iphlpapi.dll", SetLastError = true)]
    static extern IntPtr IcmpCreateFile();

    [DllImport("iphlpapi.dll", SetLastError = true)]
    static extern bool IcmpCloseHandle(IntPtr handle);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    static extern uint IcmpSendEcho2Ex(IntPtr handle, IntPtr evt, IntPtr apc, IntPtr apcContext, uint source,
                                       uint destination, byte[] data, ushort size, IntPtr options, byte[] reply,
                                       uint replySize, uint timeout);

    static uint Addr(IPAddress a) => BitConverter.ToUInt32(a.GetAddressBytes(), 0);

    /// <summary>True when `destination` answered within `timeoutMs`, from `source`.</summary>
    public static bool Ping(IPAddress source, IPAddress destination, uint timeoutMs)
    {
        var h = IcmpCreateFile();
        if (h == IntPtr.Zero || h == new IntPtr(-1)) return false;
        try
        {
            var data = new byte[32];
            var reply = new byte[256]; /* ICMP_ECHO_REPLY + data + 8, with room to spare */
            var n = IcmpSendEcho2Ex(h, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, Addr(source), Addr(destination), data,
                                    (ushort)data.Length, IntPtr.Zero, reply, (uint)reply.Length, timeoutMs);
            if (n == 0) return false;
            // ICMP_ECHO_REPLY: Address (4), Status (4): IP_SUCCESS is 0.
            return BitConverter.ToUInt32(reply, 4) == 0 && BitConverter.ToUInt32(reply, 0) == Addr(destination);
        }
        finally { IcmpCloseHandle(h); }
    }
}
