namespace Curl.Tls;

/// <summary>
/// The <c>signature_algorithms</c> extension (RFC 8446 section 4.2.3): the signature
/// schemes, in preference order. <c>signature_algorithms_cert</c> has the same data and
/// is written by <see cref="EncodeCertificateSchemes" />.
/// </summary>
public static class SignatureAlgorithmsExtension
{
    /// <summary>Returns a <c>signature_algorithms</c> extension listing <paramref name="schemes" />.</summary>
    /// <param name="schemes">The signature scheme code points, in preference order.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(IReadOnlyList<ushort> schemes) => Encode(TlsExtensionType.SignatureAlgorithms, schemes);

    /// <summary>Returns a <c>signature_algorithms_cert</c> extension listing <paramref name="schemes" />.</summary>
    /// <param name="schemes">The signature scheme code points accepted in certificates, in preference order.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeCertificateSchemes(IReadOnlyList<ushort> schemes) => Encode(TlsExtensionType.SignatureAlgorithmsCert, schemes);

    /// <summary>Decodes <c>signature_algorithms</c> or <c>signature_algorithms_cert</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The signature scheme code points, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<IReadOnlyList<ushort>> Decode(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadUInt16List(2));
    }

    private static TlsExtension Encode(TlsExtensionType type, IReadOnlyList<ushort> schemes)
    {
        TlsWriter writer = new();
        writer.WriteUInt16List(2, schemes);
        return new TlsExtension(type, writer.ToArray());
    }
}
