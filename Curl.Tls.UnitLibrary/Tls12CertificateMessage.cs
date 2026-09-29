namespace Curl.Tls;

/// <summary>
/// The TLS 1.2 and below Certificate message (RFC 5246 section 7.4.2): DER certificates,
/// sender's first, with no request context and no per-certificate extensions.
/// </summary>
/// <param name="CertificateList">The DER certificates, in the order sent.</param>
public sealed record Tls12CertificateMessage(IReadOnlyList<byte[]> CertificateList)
{
    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded Certificate.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        writer.WriteVector(3, list =>
        {
            foreach (byte[] certificate in CertificateList)
            {
                list.WriteOpaque(3, certificate);
            }
        });
        return new HandshakeMessage(HandshakeType.Certificate, writer.ToArray()).Encode();
    }

    /// <summary>Decodes a Certificate body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <returns>The Certificate, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<Tls12CertificateMessage> Decode(byte[] body)
    {
        TlsReader reader = new(body);
        TlsReader list = reader.ReadVector(3);
        List<byte[]> certificates = [];
        while (list.HasMore)
        {
            certificates.Add(list.ReadOpaque(3));
        }

        return reader.Finish(new Tls12CertificateMessage(certificates));
    }
}
