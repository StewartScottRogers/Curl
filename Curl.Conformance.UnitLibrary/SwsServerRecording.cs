using System.Text;

namespace Curl.Conformance;

/// <summary>
/// What the sws emulation writes to its protocol dump, shared by every connection of one server:
/// the bytes each client wrote, and <c>[DISCONNECT]</c> lines when <c>connection-monitor</c> is on.
/// </summary>
/// <remarks>
/// sws keeps one monitor flag for the whole server: a request's <c>&lt;servercmd&gt;</c> sets it,
/// and the next connection close records the line and clears it.
/// </remarks>
internal sealed class SwsServerRecording
{
    private static readonly byte[] DisconnectLine = Encoding.Latin1.GetBytes("[DISCONNECT]\n");

    private readonly List<byte> bytes = [];

    private bool monitorArmed;

    /// <summary>Every byte recorded so far.</summary>
    public byte[] Bytes => [.. bytes];

    /// <summary>Records bytes a client wrote.</summary>
    /// <param name="received">The bytes.</param>
    public void Record(ReadOnlySpan<byte> received) => bytes.AddRange(received);

    /// <summary>Arms the monitor, as sws does when it reads <c>connection-monitor</c> for a request.</summary>
    public void ArmDisconnectMonitor() => monitorArmed = true;

    /// <summary>Records <c>[DISCONNECT]</c> for a closing connection when the monitor is armed, and disarms it.</summary>
    public void RecordDisconnect()
    {
        if (monitorArmed)
        {
            bytes.AddRange(DisconnectLine);
            monitorArmed = false;
        }
    }
}
