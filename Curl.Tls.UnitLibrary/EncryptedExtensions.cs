namespace Curl.Tls;

/// <summary>The EncryptedExtensions message (RFC 8446 section 4.3.1).</summary>
/// <param name="Extensions">The extensions, in the order sent.</param>
public sealed record EncryptedExtensions(IReadOnlyList<TlsExtension> Extensions)
{
    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded EncryptedExtensions.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        TlsExtensionBlock.Write(writer, Extensions);
        return new HandshakeMessage(HandshakeType.EncryptedExtensions, writer.ToArray()).Encode();
    }

    /// <summary>Decodes an EncryptedExtensions body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <returns>The EncryptedExtensions, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<EncryptedExtensions> Decode(byte[] body)
    {
        TlsReader reader = new(body);
        IReadOnlyList<TlsExtension> extensions = TlsExtensionBlock.Read(reader);
        return reader.Finish(new EncryptedExtensions(extensions));
    }
}
