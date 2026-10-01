using System.Buffers.Binary;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Compression;
using Curl.Protocol.Ssh.PacketProtection;

namespace Curl.Protocol.Ssh.Transport;

/// <summary>
/// Writes binary packets (RFC 4253 section 6): <c>packet_length</c>,
/// <c>padding_length</c>, the payload and random padding, sealed by the direction's
/// <see cref="ISshPacketProtection" />, counting each packet's sequence number. Packets are
/// unprotected until the transport installs the keys of the first <c>NEWKEYS</c>.
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

    private ISshPacketProtection protection = new SshPlainPacketProtection();

    private SshZlibCompressor? compressor;

    /// <summary>
    /// Gets the sequence number the next packet written carries: 0 for the first, wrapping
    /// after 2^32 - 1 as RFC 4253 section 6.4 requires.
    /// </summary>
    internal uint SequenceNumber { get; private set; }

    /// <summary>
    /// Starts counting again from 0, as strict key exchange requires after each
    /// <c>NEWKEYS</c> (OpenSSH's <c>PROTOCOL</c>, section 1.10).
    /// </summary>
    internal void ResetSequenceNumber() => SequenceNumber = 0;

    /// <summary>
    /// Seals every packet from now on with <paramref name="newProtection" />, the
    /// client-to-server keys taken into use once the client's <c>NEWKEYS</c> is sent, and
    /// disposes the protection it replaces.
    /// </summary>
    /// <param name="newProtection">The new keys' protection.</param>
    internal void ChangeProtection(ISshPacketProtection newProtection)
    {
        protection.Dispose();
        protection = newProtection;
    }

    /// <summary>
    /// Compresses every payload from now on into one zlib stream that lasts the session, the
    /// agreed client-to-server <c>zlib</c> or <c>zlib@openssh.com</c> taking effect.
    /// </summary>
    internal void StartCompression() => compressor = new SshZlibCompressor();

    /// <summary>
    /// Works out how many padding bytes a payload gets before any cipher is in use: the
    /// fewest, at least <see cref="MinimumPadding" />, that make the whole packet a multiple
    /// of <see cref="BlockSize" />, as libssh2 1.11.1 pads (4 to 11 bytes).
    /// </summary>
    /// <param name="payloadLength">The payload's length in bytes.</param>
    /// <returns>The padding length.</returns>
    internal static int PaddingLengthFor(int payloadLength) => PaddingLengthFor(payloadLength, BlockSize, padsPacketLengthField: true);

    /// <summary>
    /// Works out how many padding bytes a payload gets: the fewest, at least
    /// <see cref="MinimumPadding" />, that make the padded part of the packet a multiple of
    /// <paramref name="blockSize" />.
    /// </summary>
    /// <param name="payloadLength">The payload's length in bytes.</param>
    /// <param name="blockSize">The block size to pad to.</param>
    /// <param name="padsPacketLengthField">Whether the four <c>packet_length</c> bytes are part of the padded part.</param>
    /// <returns>The padding length.</returns>
    internal static int PaddingLengthFor(int payloadLength, int blockSize, bool padsPacketLengthField)
    {
        int paddedLength = (padsPacketLengthField ? sizeof(uint) : 0) + 1 + payloadLength;
        int padding = blockSize - (paddedLength % blockSize);
        return padding < MinimumPadding ? padding + blockSize : padding;
    }

    /// <summary>
    /// Frames <paramref name="message" /> as one packet, compressed once compression has
    /// started, then seals, writes and flushes it.
    /// </summary>
    /// <param name="message">The message, starting with its message number.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the packet has been flushed.</returns>
    /// <exception cref="SshConnectionLostException">The write or the flush failed.</exception>
    internal async ValueTask WriteAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> payload = compressor is null ? message : compressor.Compress(message.Span);
        int paddingLength = PaddingLengthFor(payload.Length, protection.BlockSize, protection.PadsPacketLengthField);
        byte[] packet = new byte[sizeof(uint) + 1 + payload.Length + paddingLength];
        BinaryPrimitives.WriteUInt32BigEndian(packet, (uint)(packet.Length - sizeof(uint)));
        packet[sizeof(uint)] = (byte)paddingLength;
        payload.Span.CopyTo(packet.AsSpan(sizeof(uint) + 1));
        randomSource.Fill(packet.AsSpan(packet.Length - paddingLength));

        try
        {
            await connection.WriteAsync(protection.Seal(SequenceNumber, packet), cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new SshConnectionLostException(exception);
        }

        SequenceNumber = unchecked(SequenceNumber + 1);
    }
}
