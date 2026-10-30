using System.Formats.Asn1;

namespace Curl.Extension.CertificateTransparency;

/// <summary>
/// The SCT list a certificate carries in its X.509v3 extension (RFC 6962 §3.3), and its <c>TBSCertificate</c> with that
/// extension removed, which is what the log signed (RFC 9162 §8.1.2).
/// </summary>
/// <param name="List">The <c>SignedCertificateTimestampList</c>'s TLS encoding.</param>
/// <param name="TbsCertificate">The DER <c>TBSCertificate</c> without the SCT list extension.</param>
internal sealed record EmbeddedTimestamps(ReadOnlyMemory<byte> List, ReadOnlyMemory<byte> TbsCertificate)
{
    /// <summary>The object identifier of the embedded SCT list extension (RFC 6962 §3.3).</summary>
    internal const string ExtensionOid = "1.3.6.1.4.1.11129.2.4.2";

    private static readonly Asn1Tag ExtensionsTag = new(TagClass.ContextSpecific, 3, isConstructed: true);

    /// <summary>Reads the SCT list from a DER certificate.</summary>
    /// <param name="certificate">The certificate's DER encoding.</param>
    /// <returns>The list and the reconstructed <c>TBSCertificate</c>, or <see langword="null" /> when it carries no SCT list.</returns>
    /// <exception cref="AsnContentException">The certificate is not well formed DER.</exception>
    internal static EmbeddedTimestamps? Of(ReadOnlyMemory<byte> certificate)
    {
        var tbs = new AsnReader(certificate, AsnEncodingRules.DER).ReadSequence().ReadSequence();
        var writer = new AsnWriter(AsnEncodingRules.DER);
        ReadOnlyMemory<byte>? list = null;
        using (writer.PushSequence())
        {
            while (tbs.HasData)
            {
                if (CopyField(tbs, writer) is { } found)
                {
                    list = found;
                }
            }
        }

        return list is { } embedded ? new EmbeddedTimestamps(embedded, writer.Encode()) : null;
    }

    private static ReadOnlyMemory<byte>? CopyField(AsnReader tbs, AsnWriter writer)
    {
        if (tbs.PeekTag() != ExtensionsTag)
        {
            writer.WriteEncodedValue(tbs.ReadEncodedValue().Span);
            return null;
        }

        var extensions = tbs.ReadSequence(ExtensionsTag).ReadSequence();
        ReadOnlyMemory<byte>? list = null;
        using (writer.PushSequence(ExtensionsTag))
        using (writer.PushSequence())
        {
            while (extensions.HasData)
            {
                var encoded = extensions.ReadEncodedValue();
                if (ListIn(encoded) is { } found)
                {
                    list = found;
                }
                else
                {
                    writer.WriteEncodedValue(encoded.Span);
                }
            }
        }

        return list;
    }

    private static ReadOnlyMemory<byte>? ListIn(ReadOnlyMemory<byte> encodedExtension)
    {
        var extension = new AsnReader(encodedExtension, AsnEncodingRules.DER).ReadSequence();
        if (extension.ReadObjectIdentifier() != ExtensionOid)
        {
            return null;
        }

        if (extension.PeekTag().HasSameClassAndValue(Asn1Tag.Boolean))
        {
            extension.ReadBoolean();
        }

        return new AsnReader(extension.ReadOctetString(), AsnEncodingRules.DER).ReadOctetString();
    }
}
