using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// The certificates of a <c>--cacert</c> file or <c>--capath</c> directory as curl's OpenSSL build
/// reads them: <c>CERTIFICATE</c>, <c>X509 CERTIFICATE</c> and <c>TRUSTED CERTIFICATE</c> blocks.
/// A certificate may be followed by OpenSSL's <c>X509_CERT_AUX</c> settings, which can distrust it
/// for server authentication; a <c>--cacert</c> file's are read from <c>TRUSTED CERTIFICATE</c>
/// blocks only, a <c>--capath</c> file's from any of the three.
/// </summary>
/// <param name="Certificates">Every certificate read, in the order read.</param>
/// <param name="RejectedForServerAuthentication">
/// The thumbprints of the certificates whose settings, where first listed, refuse server
/// authentication.
/// </param>
internal sealed record OpenSslCaFile(X509Certificate2Collection Certificates, IReadOnlySet<string> RejectedForServerAuthentication)
{
    private const string CertificateLabel = "CERTIFICATE";
    private const string OldCertificateLabel = "X509 CERTIFICATE";
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
        var file = Of(ReadBlocks(pem, everyLabelMayHoldSettings: false));

        // PemEncoding skips a block whose body is not base64, so the blocks read are counted
        // against the blocks begun.
        var read = file.Certificates.Count;
        var begun = BlocksBegun(pem, CertificateLabel) + BlocksBegun(pem, OldCertificateLabel) + BlocksBegun(pem, TrustedCertificateLabel);
        if (read == 0 || read != begun)
        {
            throw new CryptographicException("The file holds no certificate, or one that could not be decoded.");
        }

        return file;
    }

    /// <summary>
    /// Reads a <c>--capath</c> file as OpenSSL's hashed directory lookup does: the certificate
    /// blocks before the first one that cannot be decoded; blocks with other labels are skipped.
    /// </summary>
    /// <param name="pem">The file's text.</param>
    /// <returns>Each certificate and whether its settings refuse server authentication, in file order.</returns>
    internal static IReadOnlyList<(X509Certificate2 Certificate, bool Rejects)> ReadUpToAnUndecodableBlock(string pem)
    {
        var blocks = new List<(X509Certificate2 Certificate, bool Rejects)>();
        try
        {
            foreach (var block in ReadBlocks(pem, everyLabelMayHoldSettings: true))
            {
                blocks.Add(block);
            }
        }
        catch (CryptographicException)
        {
            // The rest of the file adds nothing.
        }

        return blocks;
    }

    /// <summary>
    /// The certificates in the order OpenSSL's store lists them; of two copies of one certificate
    /// the first one's settings count.
    /// </summary>
    /// <param name="blocks">Each certificate and whether its settings refuse server authentication.</param>
    /// <returns>The certificates and which of them are rejected.</returns>
    internal static OpenSslCaFile Of(IEnumerable<(X509Certificate2 Certificate, bool Rejects)> blocks)
    {
        var certificates = new X509Certificate2Collection();
        var listed = new HashSet<string>(StringComparer.Ordinal);
        var rejected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (certificate, rejects) in blocks)
        {
            certificates.Add(certificate);
            if (listed.Add(certificate.Thumbprint) && rejects)
            {
                rejected.Add(certificate.Thumbprint);
            }
        }

        return new OpenSslCaFile(certificates, rejected);
    }

    /// <summary>
    /// This file's certificates, then the <c>--capath</c> ones: a certificate both hold keeps this
    /// file's settings, as OpenSSL loads the file before it looks in the directory.
    /// </summary>
    /// <param name="directory">The certificates of the <c>--capath</c> directories.</param>
    /// <returns>All the certificates and which of them are rejected.</returns>
    internal OpenSslCaFile FollowedBy(OpenSslCaFile directory)
    {
        var rejected = directory.RejectedForServerAuthentication
            .Except(Certificates.Select(certificate => certificate.Thumbprint))
            .Union(RejectedForServerAuthentication)
            .ToHashSet(StringComparer.Ordinal);
        return new OpenSslCaFile([.. Certificates, .. directory.Certificates], rejected);
    }

    private static IEnumerable<(X509Certificate2 Certificate, bool Rejects)> ReadBlocks(string pem, bool everyLabelMayHoldSettings)
    {
        for (var rest = pem.AsMemory(); PemEncoding.TryFind(rest.Span, out var fields); rest = rest[fields.Location.End..])
        {
            if (ReadBlock(rest.Span, fields, everyLabelMayHoldSettings) is { } block)
            {
                yield return block;
            }
        }
    }

    private static int BlocksBegun(string pem, string label) => pem.AsSpan().Count($"-----BEGIN {label}-----");

    // OpenSSL reads a --cacert file with PEM_X509_INFO_read_bio, which ignores what follows the
    // certificate in a CERTIFICATE or X509 CERTIFICATE block, and a --capath file with
    // PEM_read_bio_X509_AUX, which reads it as settings under all three labels (measured with curl
    // 8.18.0 on OpenSSL 3.5.5, BL-5927).
    private static (X509Certificate2 Certificate, bool Rejects)? ReadBlock(ReadOnlySpan<char> text, PemFields fields, bool everyLabelMayHoldSettings)
    {
        var label = text[fields.Label];
        var trusted = label is TrustedCertificateLabel;
        if (!trusted && label is not (CertificateLabel or OldCertificateLabel))
        {
            return null;
        }

        return ReadCertificate(Convert.FromBase64String(text[fields.Base64Data].ToString()), trusted || everyLabelMayHoldSettings);
    }

    private static (X509Certificate2 Certificate, bool Rejects) ReadCertificate(byte[] der, bool readsSettings)
    {
        try
        {
            AsnDecoder.ReadEncodedValue(der, AsnEncodingRules.DER, out _, out _, out var certificateLength);
            var rejects = readsSettings && SettingsRejectServerAuthentication(der.AsMemory(certificateLength));
            return (X509CertificateLoader.LoadCertificate(der.AsSpan(0, certificateLength)), rejects);
        }
        catch (AsnContentException exception)
        {
            throw new CryptographicException("A certificate or its trust settings could not be decoded.", exception);
        }
    }

    private static bool SettingsRejectServerAuthentication(ReadOnlyMemory<byte> afterCertificate)
    {
        var settings = new AsnReader(afterCertificate, AsnEncodingRules.DER);
        return settings.HasData && RejectsServerAuthentication(settings.ReadSequence());
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
