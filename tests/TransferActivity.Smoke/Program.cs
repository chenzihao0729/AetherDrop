using AetherDrop.Service;

var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
var leases = new TransferActivityLeases();
Assert(!leases.Any(start), "idle service permits normal upstream channel following");
// A large video can have no network payload during preparation. UI heartbeat
// preserves its session rather than mistaking the absence of payload for idle.
for (int seconds = 0; seconds < 600; seconds += 5)
{
    leases.Update("receiver", true, start.AddSeconds(seconds));
    Assert(leases.Any(start.AddSeconds(seconds + 4)), "preparing or stalled session remains protected", true);
}
Console.WriteLine("PASS ten-minute preparation/stall remains protected with five-second renewal");
leases.Update("sender", true, start.AddSeconds(600));
leases.Update("receiver", false, start.AddSeconds(600));
Assert(leases.Any(start.AddSeconds(601)), "one client cannot release another client's transfer");
leases.Update("sender", false, start.AddSeconds(602));
Assert(!leases.Any(start.AddSeconds(602)), "all transfers finishing releases protection immediately");
leases.Update("crashed-client", true, start.AddSeconds(610));
Assert(leases.Any(start.AddSeconds(629)), "short gaps in renewal do not interrupt a transfer");
Assert(!leases.Any(start.AddSeconds(630)), "client crash cannot leave the channel locked forever");
leases.Update("disconnected-client", true, start.AddSeconds(640));
leases.Update("disconnected-client", false, start.AddSeconds(641));
Assert(!leases.Any(start.AddSeconds(641)), "pipe disconnect releases its own session immediately");

static void Assert(bool value, string message, bool quiet = false)
{
    if (!value) throw new Exception(message);
    if (!quiet) Console.WriteLine("PASS " + message);
}
