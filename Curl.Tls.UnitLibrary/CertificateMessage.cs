namespace Curl.Tls;

/// <summary>The TLS 1.3 Certificate message (RFC 8446 section 4.4.2).</summary>
/// <param name="CertificateRequestContext">Empty from a server; the CertificateRequest's context from a client.</param>
/// <param name="CertificateList">The certificates, the end-entity certificate first.</param>
public sealed record CertificateMessage(byte[] CertificateRequestContext, IReadOnlyList<CertificateEntry> CertificateList)
{
    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded Certificate message.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        writer.WriteOpaque(1, CertificateRequestContext);
        writer.WriteVector(3, list =>
        {
            foreach (CertificateEntry entry in CertificateList)
            {
                list.WriteOpaque(3, entry.CertificateData);
                TlsExtensionBlock.Write(list, entry.Extensions);
            }
        });
        return new HandshakeMessage(HandshakeType.Certificate, writer.ToArray()).Encode();
    }

    /// <summary>Decodes a Certificate body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <returns>The Certificate message, or the alert the bytes call for (<see cref="TlsAlertDescription.DecodeError" /> for an entry with empty <c>cert_data</c>).</returns>
    public static TlsDecodeResult<CertificateMessage> Decode(byte[] body)
    {
        TlsReader reader = new(body);
        byte[] context = reader.ReadOpaque(1);
        TlsReader list = reader.ReadVector(3);
        List<CertificateEntry> entries = [];
        while (list.HasMore)
        {
            byte[] data = list.ReadNonEmptyOpaque(3);
            entries.Add(new CertificateEntry(data, TlsExtensionBlock.Read(list)));
        }

        return reader.Finish(new CertificateMessage(context, entries));
    }
}
