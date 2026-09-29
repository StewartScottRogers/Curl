namespace Curl.Tls;

/// <summary>
/// The TLS 1.2 and below CertificateVerify message (RFC 5246 section 7.4.8): the client's
/// signature over every handshake message so far, behind its scheme in TLS 1.2.
/// </summary>
/// <param name="SignatureAlgorithm">The signature scheme in TLS 1.2; <see langword="null" /> below it.</param>
/// <param name="Signature">The signature.</param>
public sealed record Tls12CertificateVerify(ushort? SignatureAlgorithm, byte[] Signature)
{
    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded CertificateVerify.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        if (SignatureAlgorithm is { } algorithm)
        {
            writer.WriteUInt16(algorithm);
        }

        writer.WriteOpaque(2, Signature);
        return new HandshakeMessage(HandshakeType.CertificateVerify, writer.ToArray()).Encode();
    }

    /// <summary>Decodes a CertificateVerify body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <param name="hasSignatureAlgorithm"><see langword="true" /> in TLS 1.2, whose signature names its scheme.</param>
    /// <returns>The CertificateVerify, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<Tls12CertificateVerify> Decode(byte[] body, bool hasSignatureAlgorithm)
    {
        TlsReader reader = new(body);
        ushort? algorithm = hasSignatureAlgorithm ? reader.ReadUInt16() : null;
        return reader.Finish(new Tls12CertificateVerify(algorithm, reader.ReadOpaque(2)));
    }
}
