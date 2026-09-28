using System.Buffers.Binary;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Transport;

/// <summary>
/// Writes unencrypted binary packets (RFC 4253 section 6): <c>packet_length</c>,
/// <c>padding_length</c>, the payload and random padding, counting each packet's sequence
/// number.
/// </summary>
/// <param name="connection">The connection to write to.</param>
/// <param name="randomSource">Where the padding bytes come from.</param>
internal sealed class SshPacketWriter(IConnection connection, ISshRandomSource randomSource)
{
    /// <summary>
    /// The block size packets are padded to before any cipher is in use: RFC 4253's
    /// minimum of 8.
    /// </summary>
    internal const int BlockSize = 8;

    /// <summary>The fewest padding bytes RFC 4253 allows.</summary>
    internal const int MinimumPadding = 4;

    /// <summary>
    /// Gets the sequence number the next packet written carries: 0 for the first, wrapping
    /// after 2^32 - 1 as RFC 4253 section 6.4 requires.
    /// </summary>
    internal uint SequenceNumber { get; private set; }

    /// <summary>
    /// Works out how many padding bytes a payload gets: the fewest, at least
    /// <see cref="MinimumPadding" />, that make the whole packet a multiple of
    /// <see cref="BlockSize" />, as libssh2 1.11.1 pads (4 to 11 bytes).
    /// </summary>
    /// <param name="payloadLength">The payload's length in bytes.</param>
    /// <returns>The padding length.</returns>
    internal static int PaddingLengthFor(int payloadLength)
    {
        int padding = BlockSize - ((sizeof(uint) + 1 + payloadLength) % BlockSize);
        return padding < MinimumPadding ? padding + BlockSize : padding;
    }

    /// <summary>
    /// Frames <paramref name="payload" /> as one packet, writes and flushes it.
    /// </summary>
    /// <param name="payload">The message, starting with its message number.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the packet has been flushed.</returns>
    internal async ValueTask WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        int paddingLength = PaddingLengthFor(payload.Length);
        byte[] packet = new byte[sizeof(uint) + 1 + payload.Length + paddingLength];
        BinaryPrimitives.WriteUInt32BigEndian(packet, (uint)(packet.Length - sizeof(uint)));
        packet[sizeof(uint)] = (byte)paddingLength;
        payload.Span.CopyTo(packet.AsSpan(sizeof(uint) + 1));
        randomSource.Fill(packet.AsSpan(packet.Length - paddingLength));

        await connection.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        SequenceNumber = unchecked(SequenceNumber + 1);
    }
}
