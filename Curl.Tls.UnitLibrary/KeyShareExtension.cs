namespace Curl.Tls;

/// <summary>
/// The <c>key_share</c> extension (RFC 8446 section 4.2.8), which has a different shape in
/// each message: a list of shares in a ClientHello, one share in a ServerHello, and the
/// selected group alone in a HelloRetryRequest.
/// </summary>
public static class KeyShareExtension
{
    /// <summary>Returns a ClientHello's <c>key_share</c> offering <paramref name="shares" />.</summary>
    /// <param name="shares">The key shares, in preference order.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeClientShares(IReadOnlyList<KeyShareEntry> shares)
    {
        ArgumentNullException.ThrowIfNull(shares);
        TlsWriter writer = new();
        writer.WriteVector(2, list =>
        {
            foreach (KeyShareEntry share in shares)
            {
                WriteEntry(list, share);
            }
        });
        return new TlsExtension(TlsExtensionType.KeyShare, writer.ToArray());
    }

    /// <summary>Decodes a ClientHello's <c>key_share</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The key shares, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<IReadOnlyList<KeyShareEntry>> DecodeClientShares(byte[] data)
    {
        TlsReader reader = new(data);
        TlsReader list = reader.ReadVector(2);
        List<KeyShareEntry> shares = [];
        while (list.HasMore)
        {
            shares.Add(ReadEntry(list));
        }

        return reader.Finish<IReadOnlyList<KeyShareEntry>>(shares);
    }

    /// <summary>Returns a ServerHello's <c>key_share</c> holding <paramref name="share" />.</summary>
    /// <param name="share">The server's key share.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeServerShare(KeyShareEntry share)
    {
        TlsWriter writer = new();
        WriteEntry(writer, share);
        return new TlsExtension(TlsExtensionType.KeyShare, writer.ToArray());
    }

    /// <summary>Decodes a ServerHello's <c>key_share</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The server's key share, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<KeyShareEntry> DecodeServerShare(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(ReadEntry(reader));
    }

    /// <summary>Returns a HelloRetryRequest's <c>key_share</c> selecting <paramref name="group" />.</summary>
    /// <param name="group">The named group the server asks the client to share.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeSelectedGroup(ushort group)
    {
        TlsWriter writer = new();
        writer.WriteUInt16(group);
        return new TlsExtension(TlsExtensionType.KeyShare, writer.ToArray());
    }

    /// <summary>Decodes a HelloRetryRequest's <c>key_share</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The selected named group, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<ushort> DecodeSelectedGroup(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadUInt16());
    }

    private static void WriteEntry(TlsWriter writer, KeyShareEntry share)
    {
        writer.WriteUInt16(share.Group);
        writer.WriteOpaque(2, share.KeyExchange);
    }

    private static KeyShareEntry ReadEntry(TlsReader reader)
    {
        ushort group = reader.ReadUInt16();
        return new KeyShareEntry(group, reader.ReadOpaque(2));
    }
}
