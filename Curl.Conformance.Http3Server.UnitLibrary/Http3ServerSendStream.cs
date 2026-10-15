using Curl.Quic;

namespace Curl.Conformance.Http3Server;

/// <summary>
/// One stream as <see cref="Http3ServerConnection" /> sends on it: the bytes written and not
/// yet sent, the FIN still to send, and the client's MAX_STREAM_DATA limit, which no STREAM
/// frame passes (RFC 9000 section 4.1).
/// </summary>
/// <param name="id">The stream ID.</param>
/// <param name="limit">The client's initial limit for the stream.</param>
internal sealed class Http3ServerSendStream(ulong id, ulong limit)
{
    private readonly List<byte> pending = [];

    private bool finPending;

    /// <summary>Gets the client's limit: the offset no byte of the stream is sent at or beyond.</summary>
    public ulong Limit { get; private set; } = limit;

    /// <summary>Gets the offset of the next byte to send.</summary>
    public ulong Offset { get; private set; }

    /// <summary>Queues bytes after those written before, and the FIN after them when <paramref name="endStream" /> is set.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="endStream">Whether the stream ends after them.</param>
    public void Append(ReadOnlySpan<byte> bytes, bool endStream)
    {
        pending.AddRange(bytes);
        finPending |= endStream;
    }

    /// <summary>Raises the limit, as a MAX_STREAM_DATA frame asks; a lower one is ignored (RFC 9000 section 4.1).</summary>
    /// <param name="newLimit">The limit the frame carries.</param>
    public void Raise(ulong newLimit) => Limit = Math.Max(Limit, newLimit);

    /// <summary>
    /// Takes the next STREAM frame: at most <see cref="Http3ServerPacketSpace.MaximumChunkLength" />
    /// bytes, within the stream's limit and <paramref name="connectionAvailable" />, carrying the
    /// FIN once its last byte goes.
    /// </summary>
    /// <param name="connectionAvailable">The bytes the client's MAX_DATA still allows on the connection.</param>
    /// <returns>The frame, or <see langword="null" /> when nothing may be sent now.</returns>
    public QuicStreamFrame? TakeFrame(ulong connectionAvailable)
    {
        var credit = Math.Min(Limit - Offset, connectionAvailable);
        var length = (int)Math.Min((ulong)Math.Min(pending.Count, Http3ServerPacketSpace.MaximumChunkLength), credit);
        var fin = finPending && length == pending.Count;
        if (length == 0 && !fin)
        {
            return null;
        }

        var frame = new QuicStreamFrame(id, Offset, pending.GetRange(0, length).ToArray(), fin);
        pending.RemoveRange(0, length);
        Offset += (ulong)length;
        finPending &= !fin;
        return frame;
    }
}
