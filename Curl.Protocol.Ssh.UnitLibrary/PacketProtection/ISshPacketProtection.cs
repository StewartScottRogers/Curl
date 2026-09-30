namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// Protects the binary packets of one direction (RFC 4253 section 6): seals each packet
/// the writer frames and opens each packet the reader receives, with the cipher and MAC,
/// or the AEAD cipher, agreed for that direction. <see cref="SshPlainPacketProtection" />
/// is what both directions use until the first <c>NEWKEYS</c>.
/// </summary>
/// <remarks>
/// The packet layer reads <see cref="LengthBlockLength" /> bytes, asks
/// <see cref="DecryptPacketLength" /> for <c>packet_length</c>, reads the rest of the
/// packet and the <see cref="TagLength" /> bytes after it, then calls
/// <see cref="Open" />. A cipher or MAC added later (BL-680) is one more
/// implementation, or one more <c>ISshCipher</c> or MAC row, not a new packet layer.
/// </remarks>
internal interface ISshPacketProtection : IDisposable
{
    /// <summary>
    /// Gets the block size packets are padded to: the cipher's, and never less than 8.
    /// </summary>
    int BlockSize { get; }

    /// <summary>
    /// Gets a value indicating whether the four <c>packet_length</c> bytes count towards
    /// the block alignment, as RFC 4253 section 6 frames packets. They do not when the
    /// length travels unencrypted beside the ciphertext, under encrypt-then-MAC and AES-GCM.
    /// </summary>
    bool PadsPacketLengthField { get; }

    /// <summary>
    /// Gets how many bytes are read before <c>packet_length</c> can be known: 4 when the
    /// length is sent unencrypted, else the cipher's first block.
    /// </summary>
    int LengthBlockLength { get; }

    /// <summary>
    /// Gets how many bytes of MAC or authentication tag follow each packet.
    /// </summary>
    int TagLength { get; }

    /// <summary>
    /// Encrypts and authenticates one framed packet.
    /// </summary>
    /// <param name="sequenceNumber">The packet's sequence number.</param>
    /// <param name="packet">
    /// The unencrypted packet: <c>packet_length</c>, <c>padding_length</c>, payload and
    /// padding.
    /// </param>
    /// <returns>The bytes to send: the protected packet, then its MAC or tag.</returns>
    byte[] Seal(uint sequenceNumber, ReadOnlySpan<byte> packet);

    /// <summary>
    /// Decrypts, in place, the first <see cref="LengthBlockLength" /> bytes of a packet
    /// when its length is encrypted, and returns its <c>packet_length</c>.
    /// </summary>
    /// <param name="sequenceNumber">The packet's sequence number.</param>
    /// <param name="lengthBlock">The packet's first <see cref="LengthBlockLength" /> bytes, as received.</param>
    /// <returns>The <c>packet_length</c> field.</returns>
    uint DecryptPacketLength(uint sequenceNumber, byte[] lengthBlock);

    /// <summary>
    /// Checks the MAC or tag of a packet and decrypts the rest of it.
    /// </summary>
    /// <param name="sequenceNumber">The packet's sequence number.</param>
    /// <param name="lengthBlock">The packet's first bytes, after <see cref="DecryptPacketLength" />.</param>
    /// <param name="remainder">The rest of the packet, as received, then its MAC or tag.</param>
    /// <returns>The packet after its <c>packet_length</c>: <c>padding_length</c>, payload and padding.</returns>
    /// <exception cref="SshPacketAuthenticationException">The MAC or tag does not match.</exception>
    byte[] Open(uint sequenceNumber, byte[] lengthBlock, byte[] remainder);
}
