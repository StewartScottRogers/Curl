using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// A <c>--cacert</c> file as curl's OpenSSL build reads it: its <c>CERTIFICATE</c> blocks and its
/// <c>TRUSTED CERTIFICATE</c> blocks, each a certificate followed by OpenSSL's <c>X509_CERT_AUX</c>
/// settings, which can distrust the certificate for server authentication.
/// </summary>
/// <param name="Certificates">Every certificate of the file, in file order.</param>
/// <param name="RejectedForServerAuthentication">
/// The thumbprints of the certificates whose settings, where the file first lists them, refuse
/// server authentication.
/// </param>
internal sealed record OpenSslCaFile(X509Certificate2Collection Certificates, IReadOnlySet<string> RejectedForServerAuthentication)
{
    private const string CertificateLabel = "CERTIFICATE";
    private const string TrustedCertificateLabel = "TRUSTED CERTIFICATE";
    private const string ServerAuthentication = "1.3.6.1.5.5.7.3.1";
    private const string AnyExtendedKeyUsage = "2.5.29.37.0";

    private static readonly Asn1Tag RejectedUses = new(TagClass.ContextSpecific, 0, isConstructed: true);

    /// <summary>Reads the text of a <c>--cacert</c> file; blocks with other labels are skipped.</summary>
    /// <param name="pem">The file's text.</param>
    /// <returns>The certificates and which of them are rejected.</returns>
    /// <exception cref="CryptographicException">
    /// The text holds no certificate block, or one that cannot be decoded.
    /// </exception>
    internal static OpenSslCaFile Read(string pem)
    {
        var file = ReadBlocks(pem);

        // PemEncoding skips a block whose body is not base64, so the blocks read are counted
        // against the blocks begun.
        var read = file.Certificates.Count;
        if (read == 0 || read != BlocksBegun(pem, CertificateLabel) + BlocksBegun(pem, TrustedCertificateLabel))
        {
            throw new CryptographicException("The file holds no certificate, or one that could not be decoded.");
        }

        return file;
    }

    private static OpenSslCaFile ReadBlocks(string pem)
    {
        var certificates = new X509Certificate2Collection();
        var listed = new HashSet<string>(StringComparer.Ordinal);
        var rejected = new HashSet<string>(StringComparer.Ordinal);
        for (var rest = pem.AsMemory(); PemEncoding.TryFind(rest.Span, out var fields); rest = rest[fields.Location.End..])
        {
            if (ReadBlock(rest.Span, fields) is not var (certificate, rejects))
            {
                continue;
            }

            certificates.Add(certificate);

            // OpenSSL's store keeps the first of two copies of a certificate, settings and all.
            if (listed.Add(certificate.Thumbprint) && rejects)
            {
                rejected.Add(certificate.Thumbprint);
            }
        }

        return new OpenSslCaFile(certificates, rejected);
    }

    private static int BlocksBegun(string pem, string label) => pem.AsSpan().Count($"-----BEGIN {label}-----");

    private static (X509Certificate2 Certificate, bool Rejects)? ReadBlock(ReadOnlySpan<char> text, PemFields fields) =>
        text[fields.Label] switch
        {
            CertificateLabel => (X509Certificate2.CreateFromPem(text[fields.Location]), false),
            TrustedCertificateLabel => ReadTrustedCertificate(Convert.FromBase64String(text[fields.Base64Data].ToString())),
            _ => null,
        };

    private static (X509Certificate2 Certificate, bool Rejects) ReadTrustedCertificate(byte[] der)
    {
        try
        {
            AsnDecoder.ReadEncodedValue(der, AsnEncodingRules.DER, out _, out _, out var certificateLength);
            var settings = new AsnReader(der.AsMemory(certificateLength), AsnEncodingRules.DER);
            var rejects = settings.HasData && RejectsServerAuthentication(settings.ReadSequence());
            return (X509CertificateLoader.LoadCertificate(der.AsSpan(0, certificateLength)), rejects);
        }
        catch (AsnContentException exception)
        {
            throw new CryptographicException("A trusted certificate could not be decoded.", exception);
        }
    }

    // OpenSSL's obj_trust: a rejected use naming server authentication or any use refuses it, as
    // does a list of trusted uses naming neither (measured with openssl verify 3.5, BL-5879). The
    // alias, key id and other fields that may follow do not matter here.
    private static bool RejectsServerAuthentication(AsnReader settings)
    {
        var trustsOtherUsesOnly = NextIs(settings, Asn1Tag.Sequence) && !NamesServerAuthentication(settings.ReadSequence());
        var rejects = NextIs(settings, RejectedUses) && NamesServerAuthentication(settings.ReadSequence(RejectedUses));
        return trustsOtherUsesOnly || rejects;
    }

    private static bool NextIs(AsnReader reader, Asn1Tag tag) => reader.HasData && reader.PeekTag() == tag;

    private static bool NamesServerAuthentication(AsnReader uses)
    {
        var named = false;
        while (uses.HasData)
        {
            named |= uses.ReadObjectIdentifier() is ServerAuthentication or AnyExtendedKeyUsage;
        }

        return named;
    }
}
