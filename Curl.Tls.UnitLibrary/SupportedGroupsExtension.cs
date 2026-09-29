namespace Curl.Tls;

/// <summary>The <c>supported_groups</c> extension (RFC 8446 section 4.2.7): the named groups, in preference order.</summary>
public static class SupportedGroupsExtension
{
    /// <summary>Returns a <c>supported_groups</c> extension listing <paramref name="groups" />.</summary>
    /// <param name="groups">The named group code points, in preference order.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(IReadOnlyList<ushort> groups)
    {
        TlsWriter writer = new();
        writer.WriteUInt16List(2, groups);
        return new TlsExtension(TlsExtensionType.SupportedGroups, writer.ToArray());
    }

    /// <summary>Decodes <c>supported_groups</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The named group code points, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<IReadOnlyList<ushort>> Decode(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadUInt16List(2));
    }
}
