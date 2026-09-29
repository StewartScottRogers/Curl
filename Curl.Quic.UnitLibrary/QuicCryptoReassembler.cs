using System.Buffers;

namespace Curl.Quic;

/// <summary>
/// Puts the CRYPTO frames of one encryption level back in order (RFC 9000 section 19.6):
/// each frame's bytes are held until every byte before them has arrived, then handed on
/// once, so TLS reads the handshake as the peer wrote it. Bytes already handed on are
/// dropped when a frame repeats them.
/// </summary>
public sealed class QuicCryptoReassembler
{
    /// <summary>How far past the bytes already handed on a frame may reach before it is refused, as ngtcp2 limits its buffer.</summary>
    public const ulong MaximumBufferedBytes = 65536;

    private readonly SortedDictionary<ulong, ReadOnlyMemory<byte>> pending = [];

    private ulong deliveredOffset;

    /// <summary>
    /// Takes one CRYPTO frame and returns the bytes that are now in order and not handed on
    /// before: empty when the frame only filled a later gap or repeated old bytes.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The next bytes of the stream, in order.</returns>
    /// <exception cref="QuicTransportException">The frame reaches more than <see cref="MaximumBufferedBytes" /> past the bytes handed on (<see cref="QuicTransportErrorCode.CryptoBufferExceeded" />).</exception>
    public byte[] Receive(QuicCryptoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var end = frame.Offset + (ulong)frame.Data.Length;
        if (end > deliveredOffset + MaximumBufferedBytes)
        {
            throw new QuicTransportException(QuicTransportErrorCode.CryptoBufferExceeded, $"A CRYPTO frame ends at offset {end}, more than {MaximumBufferedBytes} bytes past the {deliveredOffset} read.");
        }

        if (end > deliveredOffset)
        {
            Hold(frame.Offset, frame.Data);
        }

        return Deliver();
    }

    private void Hold(ulong offset, ReadOnlyMemory<byte> data)
    {
        if (!pending.TryGetValue(offset, out var held) || held.Length < data.Length)
        {
            pending[offset] = data.ToArray();
        }
    }

    private byte[] Deliver()
    {
        var delivered = new ArrayBufferWriter<byte>();
        while (pending.Count > 0 && pending.First().Key <= deliveredOffset)
        {
            var (offset, data) = pending.First();
            pending.Remove(offset);
            var end = offset + (ulong)data.Length;
            if (end > deliveredOffset)
            {
                delivered.Write(data.Span[(int)(deliveredOffset - offset)..]);
                deliveredOffset = end;
            }
        }

        return delivered.WrittenSpan.ToArray();
    }
}
