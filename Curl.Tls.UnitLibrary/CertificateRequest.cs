namespace Curl.Tls;

/// <summary>The CertificateRequest message (RFC 8446 section 4.3.2).</summary>
/// <param name="CertificateRequestContext">The context the client echoes in its Certificate.</param>
/// <param name="Extensions">The extensions, in the order sent.</param>
public sealed record CertificateRequest(byte[] CertificateRequestContext, IReadOnlyList<TlsExtension> Extensions)
{
    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded CertificateRequest.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        writer.WriteOpaque(1, CertificateRequestContext);
        TlsExtensionBlock.Write(writer, Extensions);
        return new HandshakeMessage(HandshakeType.CertificateRequest, writer.ToArray()).Encode();
    }

    /// <summary>Decodes a CertificateRequest body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <returns>The CertificateRequest, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<CertificateRequest> Decode(byte[] body)
    {
        TlsReader reader = new(body);
        byte[] context = reader.ReadOpaque(1);
        IReadOnlyList<TlsExtension> extensions = TlsExtensionBlock.Read(reader);
        return reader.Finish(new CertificateRequest(context, extensions));
    }
}
