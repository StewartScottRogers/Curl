namespace Curl.Extended.Protocol.Coap.Dtls.Wire;

/// <summary>
/// The anti-replay sliding window of RFC 6347 section 4.1.2.6 for one epoch: a record whose
/// sequence number was already received, or is older than the window's left edge, is a replay.
/// The receiver asks <see cref="IsReplay" /> before verifying a record and calls
/// <see cref="MarkReceived" /> only once the record's MAC or AEAD tag verifies.
/// </summary>
internal sealed class DtlsReplayWindow
{
    /// <summary>How many sequence numbers the window holds, the minimum RFC 6347 recommends.</summary>
    public const int Size = 64;

    private ulong highest;

    // Bit i set: sequence number (highest - i) was received.
    private ulong received;

    private bool anyReceived;

    /// <summary>Whether a record with <paramref name="sequenceNumber" /> must be discarded as a replay.</summary>
    public bool IsReplay(ulong sequenceNumber)
    {
        if (!anyReceived || sequenceNumber > highest)
        {
            return false;
        }

        ulong offset = highest - sequenceNumber;
        return offset >= Size || (received & (1UL << (int)offset)) != 0;
    }

    /// <summary>
    /// Records <paramref name="sequenceNumber" /> as received, advancing the window when it is past
    /// the highest so far. A number older than the window changes nothing.
    /// </summary>
    public void MarkReceived(ulong sequenceNumber)
    {
        if (!anyReceived || sequenceNumber > highest)
        {
            ulong shift = anyReceived ? sequenceNumber - highest : Size;
            received = shift >= Size ? 1UL : (received << (int)shift) | 1UL;
            highest = sequenceNumber;
            anyReceived = true;
            return;
        }

        ulong offset = highest - sequenceNumber;
        if (offset < Size)
        {
            received |= 1UL << (int)offset;
        }
    }
}
