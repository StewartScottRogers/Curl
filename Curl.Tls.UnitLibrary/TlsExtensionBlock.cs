namespace Curl.Tls;

/// <summary>
/// Reads and writes a handshake message's extension block: a 16-bit length, then each
/// extension's type and 16-bit-length data. A type that appears twice in one block
/// records <see cref="TlsAlertDescription.IllegalParameter" /> (RFC 8446 section 4.2).
/// </summary>
internal static class TlsExtensionBlock
{
    public static void Write(TlsWriter writer, IReadOnlyList<TlsExtension> extensions) => writer.WriteVector(2, block =>
    {
        foreach (TlsExtension extension in extensions)
        {
            block.WriteUInt16((ushort)extension.Type);
            block.WriteOpaque(2, extension.Data);
        }
    });

    public static IReadOnlyList<TlsExtension> Read(TlsReader reader)
    {
        TlsReader block = reader.ReadVector(2);
        List<TlsExtension> extensions = [];
        HashSet<TlsExtensionType> seen = [];
        while (block.HasMore)
        {
            TlsExtensionType type = (TlsExtensionType)block.ReadUInt16();
            byte[] data = block.ReadOpaque(2);
            if (!seen.Add(type))
            {
                block.Fail(TlsAlertDescription.IllegalParameter);
            }

            extensions.Add(new TlsExtension(type, data));
        }

        return extensions;
    }
}
