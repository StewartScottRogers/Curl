namespace Curl.Tls;

/// <summary>
/// The <c>supported_versions</c> extension (RFC 8446 section 4.2.1): the versions offered
/// in a ClientHello, and the one selected in a ServerHello or HelloRetryRequest.
/// </summary>
public static class SupportedVersionsExtension
{
    /// <summary>Returns a ClientHello's <c>supported_versions</c> offering <paramref name="versions" />.</summary>
    /// <param name="versions">The version code points (<c>0x0304</c> for TLS 1.3), in preference order.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeOffered(IReadOnlyList<ushort> versions)
    {
        TlsWriter writer = new();
        writer.WriteUInt16List(1, versions);
        return new TlsExtension(TlsExtensionType.SupportedVersions, writer.ToArray());
    }

    /// <summary>Decodes a ClientHello's <c>supported_versions</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The offered versions, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<IReadOnlyList<ushort>> DecodeOffered(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadUInt16List(1));
    }

    /// <summary>Returns a ServerHello's <c>supported_versions</c> selecting <paramref name="version" />.</summary>
    /// <param name="version">The selected version code point.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeSelected(ushort version)
    {
        TlsWriter writer = new();
        writer.WriteUInt16(version);
        return new TlsExtension(TlsExtensionType.SupportedVersions, writer.ToArray());
    }

    /// <summary>Decodes a ServerHello's <c>supported_versions</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The selected version, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<ushort> DecodeSelected(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadUInt16());
    }
}
