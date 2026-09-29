namespace Curl.Tls;

/// <summary>
/// The <c>certificate_authorities</c> extension (RFC 8446 section 4.2.4): the DER-encoded
/// distinguished names of the certificate authorities the sender accepts.
/// </summary>
public static class CertificateAuthoritiesExtension
{
    /// <summary>Returns a <c>certificate_authorities</c> extension listing <paramref name="distinguishedNames" />.</summary>
    /// <param name="distinguishedNames">The DER-encoded distinguished names.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(IReadOnlyList<byte[]> distinguishedNames)
    {
        ArgumentNullException.ThrowIfNull(distinguishedNames);
        TlsWriter writer = new();
        writer.WriteVector(2, list =>
        {
            foreach (byte[] distinguishedName in distinguishedNames)
            {
                list.WriteOpaque(2, distinguishedName);
            }
        });
        return new TlsExtension(TlsExtensionType.CertificateAuthorities, writer.ToArray());
    }

    /// <summary>Decodes <c>certificate_authorities</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The DER-encoded distinguished names, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<IReadOnlyList<byte[]>> Decode(byte[] data)
    {
        TlsReader reader = new(data);
        TlsReader list = reader.ReadVector(2);
        List<byte[]> distinguishedNames = [];
        while (list.HasMore)
        {
            distinguishedNames.Add(list.ReadOpaque(2));
        }

        return reader.Finish<IReadOnlyList<byte[]>>(distinguishedNames);
    }
}
