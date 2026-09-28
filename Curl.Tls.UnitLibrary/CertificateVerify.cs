namespace Curl.Tls;

/// <summary>The CertificateVerify message (RFC 8446 section 4.4.3).</summary>
/// <param name="Algorithm">The signature scheme code point.</param>
/// <param name="Signature">The signature over the transcript.</param>
public sealed record CertificateVerify(ushort Algorithm, byte[] Signature)
{
    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded CertificateVerify.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        writer.WriteUInt16(Algorithm);
        writer.WriteOpaque(2, Signature);
        return new HandshakeMessage(HandshakeType.CertificateVerify, writer.ToArray()).Encode();
    }

    /// <summary>Decodes a CertificateVerify body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <returns>The CertificateVerify, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<CertificateVerify> Decode(byte[] body)
    {
        TlsReader reader = new(body);
        ushort algorithm = reader.ReadUInt16();
        byte[] signature = reader.ReadOpaque(2);
        return reader.Finish(new CertificateVerify(algorithm, signature));
    }
}
