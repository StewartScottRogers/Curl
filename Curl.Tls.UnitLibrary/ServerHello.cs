namespace Curl.Tls;

/// <summary>
/// The ServerHello message (RFC 8446 section 4.1.3), which also carries a
/// HelloRetryRequest (section 4.1.4): the same message with the fixed
/// <see cref="HelloRetryRequestRandom" /> as its random.
/// </summary>
/// <param name="LegacyVersion">The <c>legacy_version</c> field (<c>0x0303</c> in TLS 1.3).</param>
/// <param name="Random">The 32-byte server random.</param>
/// <param name="LegacySessionIdEcho">The client's <c>legacy_session_id</c>, echoed.</param>
/// <param name="CipherSuite">The chosen cipher suite.</param>
/// <param name="LegacyCompressionMethod">The chosen compression method (0 in TLS 1.3).</param>
/// <param name="Extensions">The extensions, in the order sent.</param>
public sealed record ServerHello(
    ushort LegacyVersion,
    byte[] Random,
    byte[] LegacySessionIdEcho,
    ushort CipherSuite,
    byte LegacyCompressionMethod,
    IReadOnlyList<TlsExtension> Extensions)
{
    /// <summary>Gets the random that marks a HelloRetryRequest: SHA-256 of "HelloRetryRequest".</summary>
    public static ReadOnlySpan<byte> HelloRetryRequestRandom =>
    [
        0xcf, 0x21, 0xad, 0x74, 0xe5, 0x9a, 0x61, 0x11, 0xbe, 0x1d, 0x8c, 0x02, 0x1e, 0x65, 0xb8, 0x91,
        0xc2, 0xa2, 0x11, 0x16, 0x7a, 0xbb, 0x8c, 0x5e, 0x07, 0x9e, 0x09, 0xe2, 0xc8, 0xa8, 0x33, 0x9c,
    ];

    /// <summary>Gets a value indicating whether this message is a HelloRetryRequest.</summary>
    public bool IsHelloRetryRequest => Random.AsSpan().SequenceEqual(HelloRetryRequestRandom);

    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded ServerHello.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        writer.WriteUInt16(LegacyVersion);
        writer.WriteBytes(Random);
        writer.WriteOpaque(1, LegacySessionIdEcho);
        writer.WriteUInt16(CipherSuite);
        writer.WriteUInt8(LegacyCompressionMethod);
        TlsExtensionBlock.Write(writer, Extensions);
        return new HandshakeMessage(HandshakeType.ServerHello, writer.ToArray()).Encode();
    }

    /// <summary>Decodes a ServerHello body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <returns>The ServerHello, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<ServerHello> Decode(byte[] body)
    {
        TlsReader reader = new(body);
        ushort legacyVersion = reader.ReadUInt16();
        byte[] random = reader.ReadBytes(ClientHello.RandomLength);
        byte[] legacySessionIdEcho = reader.ReadOpaque(1);
        ushort cipherSuite = reader.ReadUInt16();
        byte compressionMethod = reader.ReadUInt8();
        IReadOnlyList<TlsExtension> extensions = TlsExtensionBlock.Read(reader);
        return reader.Finish(new ServerHello(legacyVersion, random, legacySessionIdEcho, cipherSuite, compressionMethod, extensions));
    }
}
