using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Curl.Extension.Sigstore;

/// <summary>
/// Who a Fulcio signing certificate names (Fulcio's certificate specification, "Certificate Extensions"): the
/// e-mail addresses and URIs of its subject alternative name, and the OIDC issuer of its
/// <c>1.3.6.1.4.1.57264.1.8</c> extension or, in older certificates, the deprecated <c>1.3.6.1.4.1.57264.1.1</c>.
/// A subject alternative name or <c>1.3.6.1.4.1.57264.1.8</c> value that is not the DER it should be names nothing.
/// </summary>
internal static class SigningCertificateIdentity
{
    private const string SubjectAlternativeNameOid = "2.5.29.17";
    private const string IssuerV2Oid = "1.3.6.1.4.1.57264.1.8";
    private const string IssuerV1Oid = "1.3.6.1.4.1.57264.1.1";
    private const int Rfc822NameTag = 1;
    private const int UniformResourceIdentifierTag = 6;

    /// <summary>Gives the e-mail addresses (<c>rfc822Name</c>) and URIs the certificate's subject alternative name holds.</summary>
    /// <param name="certificate">The signing certificate.</param>
    /// <returns>The names, in certificate order; none when it has no subject alternative name or one that is not DER.</returns>
    public static IReadOnlyList<string> NamesOf(X509Certificate2 certificate) =>
        ExtensionOf(certificate, SubjectAlternativeNameOid) is { } extension ? NamesInSubjectAlternativeName(extension.RawData) : [];

    /// <summary>Gives the e-mail addresses (<c>rfc822Name</c>) and URIs of a subject alternative name extension's value.</summary>
    /// <param name="value">The extension's value, a DER <c>GeneralNames</c> sequence.</param>
    /// <returns>The names, in order; none when <paramref name="value" /> is not that DER.</returns>
    internal static IReadOnlyList<string> NamesInSubjectAlternativeName(byte[] value)
    {
        try
        {
            AsnReader reader = new(value, AsnEncodingRules.DER);
            var sequence = reader.ReadSequence();
            reader.ThrowIfNotEmpty();
            List<string> names = [];
            while (sequence.HasData)
            {
                ReadGeneralName(sequence, names);
            }

            return names;
        }
        catch (AsnContentException)
        {
            return [];
        }
    }

    private static void ReadGeneralName(AsnReader sequence, List<string> names)
    {
        var tag = sequence.PeekTag();
        if (IsEmailOrUri(tag))
        {
            names.Add(sequence.ReadCharacterString(UniversalTagNumber.IA5String, tag));
        }
        else
        {
            sequence.ReadEncodedValue();
        }
    }

    /// <summary>Gives the OIDC issuer the certificate names.</summary>
    /// <param name="certificate">The signing certificate.</param>
    /// <returns>
    /// The issuer, or <see langword="null" /> when it names none: it has neither extension, or its
    /// <c>1.3.6.1.4.1.57264.1.8</c> one is not a DER <c>UTF8String</c>.
    /// </returns>
    public static string? IssuerOf(X509Certificate2 certificate)
    {
        if (ExtensionOf(certificate, IssuerV2Oid) is { } v2)
        {
            return DerUtf8StringIn(v2.RawData);
        }

        return ExtensionOf(certificate, IssuerV1Oid) is { } v1 ? Encoding.UTF8.GetString(v1.RawData) : null;
    }

    private static string? DerUtf8StringIn(byte[] value)
    {
        try
        {
            AsnReader reader = new(value, AsnEncodingRules.DER);
            var text = reader.ReadCharacterString(UniversalTagNumber.UTF8String);
            reader.ThrowIfNotEmpty();
            return text;
        }
        catch (AsnContentException)
        {
            return null;
        }
    }

    private static bool IsEmailOrUri(Asn1Tag tag) =>
        tag.TagClass == TagClass.ContextSpecific && tag.TagValue is Rfc822NameTag or UniformResourceIdentifierTag;

    private static X509Extension? ExtensionOf(X509Certificate2 certificate, string oid) =>
        certificate.Extensions[oid];
}
