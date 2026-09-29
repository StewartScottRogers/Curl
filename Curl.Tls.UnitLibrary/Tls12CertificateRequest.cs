namespace Curl.Tls;

/// <summary>
/// The TLS 1.2 and below CertificateRequest message (RFC 5246 section 7.4.4): the
/// certificate types the server accepts, the signature schemes it accepts in the
/// CertificateVerify (TLS 1.2 only), and the DER names of the authorities it trusts.
/// </summary>
/// <param name="CertificateTypes">The <c>ClientCertificateType</c> values, such as 1 (<c>rsa_sign</c>) and 64 (<c>ecdsa_sign</c>).</param>
/// <param name="SignatureAlgorithms">The <c>supported_signature_algorithms</c> in TLS 1.2; <see langword="null" /> below it, where the field does not exist.</param>
/// <param name="CertificateAuthorities">The DER distinguished names of acceptable authorities.</param>
public sealed record Tls12CertificateRequest(
    byte[] CertificateTypes,
    IReadOnlyList<ushort>? SignatureAlgorithms,
    IReadOnlyList<byte[]> CertificateAuthorities)
{
    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded CertificateRequest.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        writer.WriteOpaque(1, CertificateTypes);
        if (SignatureAlgorithms is not null)
        {
            writer.WriteUInt16List(2, SignatureAlgorithms);
        }

        writer.WriteVector(2, list =>
        {
            foreach (byte[] name in CertificateAuthorities)
            {
                list.WriteOpaque(2, name);
            }
        });
        return new HandshakeMessage(HandshakeType.CertificateRequest, writer.ToArray()).Encode();
    }

    /// <summary>Decodes a CertificateRequest body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <param name="hasSignatureAlgorithms"><see langword="true" /> in TLS 1.2, whose request carries <c>supported_signature_algorithms</c>.</param>
    /// <returns>The CertificateRequest, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<Tls12CertificateRequest> Decode(byte[] body, bool hasSignatureAlgorithms)
    {
        TlsReader reader = new(body);
        byte[] types = reader.ReadOpaque(1);
        IReadOnlyList<ushort>? algorithms = hasSignatureAlgorithms ? reader.ReadUInt16List(2) : null;
        TlsReader list = reader.ReadVector(2);
        List<byte[]> authorities = [];
        while (list.HasMore)
        {
            authorities.Add(list.ReadOpaque(2));
        }

        return reader.Finish(new Tls12CertificateRequest(types, algorithms, authorities));
    }
}
