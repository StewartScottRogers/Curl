using System.Text;

namespace Curl.Conformance;

/// <summary>
/// What the sws emulation writes to its protocol dump, shared by every connection of one server:
/// the bytes each client wrote, and <c>[DISCONNECT]</c> lines when <c>connection-monitor</c> is on.
/// </summary>
/// <remarks>
/// sws marks each connection that served a request while <c>connection-monitor</c> was on, and that
/// connection's close records the line (BL-2178). Connections are written on many
/// threads at once when curl runs transfers in parallel, so every member takes one lock, and the
/// bytes of one write stay together (BL-1647). Each write is numbered in <see cref="Dump"/>, so
/// the runner can interleave this server's dump with the other servers' (test2003).
/// </remarks>
internal sealed class SwsServerRecording
{
    private static readonly byte[] DisconnectLine = Encoding.Latin1.GetBytes("[DISCONNECT]\n");

    private readonly Lock gate = new();

    private readonly UpstreamProtocolDump dump = new();

    /// <summary>Every byte recorded so far.</summary>
    public byte[] Bytes => dump.Bytes;

    /// <summary>Every write recorded so far, each numbered in the order of all servers' writes.</summary>
    public IReadOnlyList<UpstreamProtocolDumpEntry> Dump => dump.Entries;

    /// <summary>Records bytes a client wrote.</summary>
    /// <param name="received">The bytes.</param>
    public void Record(ReadOnlySpan<byte> received) => dump.Record(received);

    /// <summary>Forgets the last bytes recorded, ones sws never read into its protocol dump.</summary>
    /// <param name="count">How many of the last bytes to forget.</param>
    public void Forget(int count) => dump.Forget(count);

    /// <summary>Records <c>[DISCONNECT]</c> for a monitored connection that is closing.</summary>
    public void RecordDisconnect()
    {
        lock (gate)
        {
            dump.Record(DisconnectLine);
        }
    }
}
