using System.Buffers.Binary;

namespace Curl.Protocol.Ssh.Transport;

/// <summary>
/// Reads unencrypted binary packets (RFC 4253 section 6), checks their framing and counts
/// each packet's sequence number.
/// </summary>
/// <param name="reader">The buffered reader over the connection.</param>
internal sealed class SshPacketReader(SshConnectionReader reader)
{
    /// <summary>
    /// The largest whole packet accepted, <c>packet_length</c> field included: libssh2
    /// 1.11.1's <c>LIBSSH2_PACKET_MAXPAYLOAD</c>, above RFC 4253's minimum of 35000.
    /// </summary>
    internal const int MaximumPacketSize = 40000;

    /// <summary>
    /// Gets the sequence number the next packet read carries: 0 for the first, wrapping
    /// after 2^32 - 1.
    /// </summary>
    internal uint SequenceNumber { get; private set; }

    /// <summary>
    /// Starts counting again from 0, as strict key exchange requires after each
    /// <c>NEWKEYS</c> (OpenSSH's <c>PROTOCOL</c>, section 1.10).
    /// </summary>
    internal void ResetSequenceNumber() => SequenceNumber = 0;

    /// <summary>
    /// Reads one packet and returns its payload.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The payload, starting with its message number.</returns>
    /// <exception cref="EndOfStreamException">The peer closed before the packet was whole.</exception>
    /// <exception cref="InvalidDataException">
    /// The packet is larger than <see cref="MaximumPacketSize" />, not a multiple of
    /// <see cref="SshPacketWriter.BlockSize" />, has fewer than
    /// <see cref="SshPacketWriter.MinimumPadding" /> padding bytes, or has no payload.
    /// </exception>
    internal async ValueTask<byte[]> ReadAsync(CancellationToken cancellationToken)
    {
        byte[] lengthBytes = await reader.ReadExactlyAsync(sizeof(uint), cancellationToken).ConfigureAwait(false);
        uint packetLength = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
        RejectBadLength(packetLength);

        byte[] packet = await reader.ReadExactlyAsync((int)packetLength, cancellationToken).ConfigureAwait(false);
        int paddingLength = packet[0];
        int payloadLength = packet.Length - 1 - paddingLength;
        if (paddingLength < SshPacketWriter.MinimumPadding || payloadLength < 1)
        {
            throw new InvalidDataException($"The SSH packet's padding length {paddingLength} does not fit its length {packetLength}.");
        }

        SequenceNumber = unchecked(SequenceNumber + 1);
        return packet.AsSpan(1, payloadLength).ToArray();
    }

    private static void RejectBadLength(uint packetLength)
    {
        if (packetLength > MaximumPacketSize - sizeof(uint) || (packetLength + sizeof(uint)) % SshPacketWriter.BlockSize != 0)
        {
            throw new InvalidDataException($"The SSH packet length {packetLength} is not a valid unencrypted packet length.");
        }
    }
}
