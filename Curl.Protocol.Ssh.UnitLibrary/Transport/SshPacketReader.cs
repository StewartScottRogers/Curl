using Curl.Protocol.Ssh.Compression;
using Curl.Protocol.Ssh.PacketProtection;

namespace Curl.Protocol.Ssh.Transport;

/// <summary>
/// Reads binary packets (RFC 4253 section 6) through the direction's
/// <see cref="ISshPacketProtection" />, checks their framing and counts each packet's
/// sequence number. Packets are unprotected until the transport installs the keys of the
/// first <c>NEWKEYS</c>.
/// </summary>
/// <param name="reader">The buffered reader over the connection.</param>
internal sealed class SshPacketReader(SshConnectionReader reader)
{
    /// <summary>
    /// The largest whole packet accepted, <c>packet_length</c> field and MAC or tag
    /// included: libssh2 1.11.1's <c>LIBSSH2_PACKET_MAXPAYLOAD</c>, above RFC 4253's
    /// minimum of 35000. Measured 2026-10-01 (BL-1081) under <c>aes128-ctr</c> with
    /// <c>hmac-sha2-256</c>: a <c>packet_length</c> of 39964 is read, 39980 is refused.
    /// </summary>
    internal const int MaximumPacketSize = 40000;

    private ISshPacketProtection protection = new SshPlainPacketProtection();

    private SshZlibDecompressor? decompressor;

    /// <summary>
    /// Gets the sequence number the next packet read carries: 0 for the first, wrapping
    /// after 2^32 - 1.
    /// </summary>
    internal uint SequenceNumber { get; private set; }

    /// <summary>
    /// Gets or sets where each payload's message number is logged at <c>verbose</c>.
    /// </summary>
    internal SshDiagnosticLog DiagnosticLog { get; set; } = SshDiagnosticLog.None;

    /// <summary>
    /// Starts counting again from 0, as strict key exchange requires after each
    /// <c>NEWKEYS</c> (OpenSSH's <c>PROTOCOL</c>, section 1.10).
    /// </summary>
    internal void ResetSequenceNumber() => SequenceNumber = 0;

    /// <summary>
    /// Opens every packet from now on with <paramref name="newProtection" />, the
    /// server-to-client keys taken into use when the server's <c>NEWKEYS</c> arrives, and
    /// disposes the protection it replaces.
    /// </summary>
    /// <param name="newProtection">The new keys' protection.</param>
    internal void ChangeProtection(ISshPacketProtection newProtection)
    {
        protection.Dispose();
        protection = newProtection;
    }

    /// <summary>
    /// Inflates every payload from now on with one zlib stream that lasts the session, the
    /// agreed server-to-client <c>zlib</c> or <c>zlib@openssh.com</c> taking effect.
    /// </summary>
    internal void StartDecompression() => decompressor = new SshZlibDecompressor();

    /// <summary>
    /// Reads one packet and returns its payload, inflated once decompression has started.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The payload, starting with its message number.</returns>
    /// <exception cref="EndOfStreamException">The peer closed before the packet was whole.</exception>
    /// <exception cref="SshPacketLengthException">
    /// The packet's length is zero, or the packet with its MAC or tag is larger than
    /// <see cref="MaximumPacketSize" />.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// The packet is not a multiple of the
    /// protection's block size, has fewer than <see cref="SshPacketWriter.MinimumPadding" />
    /// padding bytes, or has no payload; or its payload does not inflate
    /// (<see cref="SshZlibDecompressor.Decompress" />).
    /// </exception>
    /// <exception cref="SshPacketAuthenticationException">The packet's MAC or tag does not match.</exception>
    internal async ValueTask<byte[]> ReadAsync(CancellationToken cancellationToken)
    {
        byte[] lengthBlock = await reader.ReadExactlyAsync(protection.LengthBlockLength, cancellationToken).ConfigureAwait(false);
        uint packetLength = protection.DecryptPacketLength(SequenceNumber, lengthBlock);
        RejectBadLength(packetLength);

        int remainderLength = sizeof(uint) + (int)packetLength - lengthBlock.Length + protection.TagLength;
        byte[] remainder = await reader.ReadExactlyAsync(remainderLength, cancellationToken).ConfigureAwait(false);
        byte[] packet = protection.Open(SequenceNumber, lengthBlock, remainder);
        int paddingLength = packet[0];
        int payloadLength = packet.Length - 1 - paddingLength;
        if (paddingLength < SshPacketWriter.MinimumPadding || payloadLength < 1)
        {
            throw new InvalidDataException($"The SSH packet's padding length {paddingLength} does not fit its length {packetLength}.");
        }

        SequenceNumber = unchecked(SequenceNumber + 1);
        byte[] payload = packet.AsSpan(1, payloadLength).ToArray();
        byte[] message = decompressor is null ? payload : decompressor.Decompress(payload);
        DiagnosticLog.MessageReceived(message[0]);
        return message;
    }

    // libssh2 1.11.1 refuses a zero length with -12 and a packet over the maximum, MAC or
    // tag included, with -41, as both reference builds measured (BL-1081, ADR-0206).
    private void RejectBadLength(uint packetLength)
    {
        if (packetLength == 0)
        {
            throw new SshPacketLengthException(Libssh2ErrorCode.Decrypt, packetLength);
        }

        if (sizeof(uint) + (long)packetLength + protection.TagLength > MaximumPacketSize)
        {
            throw new SshPacketLengthException(Libssh2ErrorCode.OutOfBoundary, packetLength);
        }

        uint alignedLength = protection.PadsPacketLengthField ? packetLength + sizeof(uint) : packetLength;
        if (alignedLength % protection.BlockSize != 0)
        {
            throw new InvalidDataException($"The SSH packet length {packetLength} is not a valid packet length.");
        }
    }
}
