namespace Curl.Tls;

/// <summary>
/// The ClientHello message (RFC 8446 section 4.1.2). TLS 1.3 fixes
/// <paramref name="LegacyVersion" /> at <c>0x0303</c> and the compression methods at the
/// null method alone; both stay fields so a TLS 1.2, 1.1 or 1.0 hello can be written too.
/// </summary>
/// <param name="LegacyVersion">The <c>legacy_version</c> field.</param>
/// <param name="Random">The 32-byte client random.</param>
/// <param name="LegacySessionId">The <c>legacy_session_id</c>, up to 32 bytes.</param>
/// <param name="CipherSuites">The cipher suite code points, in preference order.</param>
/// <param name="LegacyCompressionMethods">The <c>legacy_compression_methods</c>.</param>
/// <param name="Extensions">The extensions, in the order sent.</param>
public sealed record ClientHello(
    ushort LegacyVersion,
    byte[] Random,
    byte[] LegacySessionId,
    IReadOnlyList<ushort> CipherSuites,
    byte[] LegacyCompressionMethods,
    IReadOnlyList<TlsExtension> Extensions)
{
    /// <summary>The length in bytes of a hello's random.</summary>
    public const int RandomLength = 32;

    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded ClientHello.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        writer.WriteUInt16(LegacyVersion);
        writer.WriteBytes(Random);
        writer.WriteOpaque(1, LegacySessionId);
        writer.WriteUInt16List(2, CipherSuites);
        writer.WriteOpaque(1, LegacyCompressionMethods);
        TlsExtensionBlock.Write(writer, Extensions);
        return new HandshakeMessage(HandshakeType.ClientHello, writer.ToArray()).Encode();
    }

    /// <summary>Decodes a ClientHello body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <returns>The ClientHello, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<ClientHello> Decode(byte[] body)
    {
        TlsReader reader = new(body);
        ushort legacyVersion = reader.ReadUInt16();
        byte[] random = reader.ReadBytes(RandomLength);
        byte[] legacySessionId = reader.ReadOpaque(1);
        IReadOnlyList<ushort> cipherSuites = reader.ReadUInt16List(2);
        byte[] compressionMethods = reader.ReadOpaque(1);
        IReadOnlyList<TlsExtension> extensions = TlsExtensionBlock.Read(reader);
        return reader.Finish(new ClientHello(legacyVersion, random, legacySessionId, cipherSuites, compressionMethods, extensions));
    }
}
