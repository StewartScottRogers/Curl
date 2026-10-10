using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Conformance;

/// <summary>
/// Writes the certificate files upstream's cases read through <c>%CERTDIR/certs/</c>, as
/// upstream's <c>tests/certs/genserv.pl</c> does with OpenSSL at build time, from the vendored
/// <c>.prm</c> files and with the base class library only (BL-1923): a P-256 test CA from
/// <c>test-ca.prm</c>, valid 6000 days, then for every other <c>test-*.prm</c> a P-256 key and
/// a certificate valid 300 days, signed by that CA, with the subject and the <c>x509v3</c>
/// extensions its <c>.prm</c> names.
/// </summary>
/// <remarks>
/// The CA writes <c>test-ca.key</c>, <c>test-ca.cacert</c>, <c>test-ca.crt</c> and <c>test-ca.der</c>;
/// each other prefix writes <c>.key</c> (PKCS#8 PEM), <c>.pub.pem</c>, <c>.pub.der</c>,
/// <c>.crt</c>, <c>.der</c>, <c>.crl</c> (a PEM CRL from the CA revoking that certificate) and
/// <c>.pem</c> (the <c>.prm</c> text, the key and the certificate, concatenated as genserv.pl
/// does). Where OpenSSL puts a text dump before the PEM block of <c>.cacert</c> and
/// <c>.crt</c>, these files hold the PEM block alone; curl reads only the block.
/// </remarks>
public sealed class UpstreamTestCertificateGenerator(TimeProvider timeProvider)
{
    /// <summary>The prefix of the CA's files, genserv.pl's first argument.</summary>
    public const string CertificateAuthorityPrefix = "test-ca";

    private const int CertificateAuthorityDays = 6000;
    private const int CertificateDays = 300;
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";
    private const string ClientAuthOid = "1.3.6.1.5.5.7.3.2";
    private const string SubjectAlternativeNameOid = "2.5.29.17";

    /// <summary>
    /// Generates the CA from <c>test-ca.prm</c> in <paramref name="parametersFolder"/>, then every
    /// other <c>test-*.prm</c> there in ordinal order, writing the files into
    /// <paramref name="outputFolder"/> (created when missing).
    /// </summary>
    public void Generate(string parametersFolder, string outputFolder)
    {
        ArgumentNullException.ThrowIfNull(parametersFolder);
        ArgumentNullException.ThrowIfNull(outputFolder);
        Directory.CreateDirectory(outputFolder);
        using X509Certificate2 authority = GenerateCertificateAuthority(
            File.ReadAllText(Path.Combine(parametersFolder, CertificateAuthorityPrefix + ".prm")), outputFolder);
        string[] parameterFiles = Directory.GetFiles(parametersFolder, "test-*.prm");
        Array.Sort(parameterFiles, StringComparer.Ordinal);
        foreach (string parameterFile in parameterFiles)
        {
            string prefix = Path.GetFileNameWithoutExtension(parameterFile);
            if (prefix != CertificateAuthorityPrefix)
            {
                GenerateCertificate(prefix, File.ReadAllText(parameterFile), authority, outputFolder);
            }
        }
    }

    /// <summary>
    /// Writes the self-signed CA described by <paramref name="parametersText"/> into
    /// <paramref name="outputFolder"/> and returns it with its private key.
    /// </summary>
    public X509Certificate2 GenerateCertificateAuthority(string parametersText, string outputFolder)
    {
        UpstreamCertificateParameters parameters = UpstreamCertificateParameters.Parse(parametersText);
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request = CreateRequest(parameters, key, issuerKeyIdentifier: null);
        DateTimeOffset now = timeProvider.GetUtcNow();
        X509Certificate2 created = request.CreateSelfSigned(now, now.AddDays(CertificateAuthorityDays));
        string prefix = Path.Combine(outputFolder, CertificateAuthorityPrefix);
        File.WriteAllText(prefix + ".key", key.ExportPkcs8PrivateKeyPem() + "\n");
        File.WriteAllText(prefix + ".cacert", created.ExportCertificatePem() + "\n");
        File.WriteAllText(prefix + ".crt", created.ExportCertificatePem() + "\n");
        File.WriteAllBytes(prefix + ".der", created.RawData);
        return created;
    }

    /// <summary>
    /// Writes the files of <paramref name="prefix"/>, described by <paramref name="parametersText"/>
    /// and signed by <paramref name="authority"/> (which holds its private key), into <paramref name="outputFolder"/>.
    /// </summary>
    public void GenerateCertificate(string prefix, string parametersText, X509Certificate2 authority, string outputFolder)
    {
        ArgumentNullException.ThrowIfNull(authority);
        UpstreamCertificateParameters parameters = UpstreamCertificateParameters.Parse(parametersText);
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa authorityKey = authority.GetECDsaPrivateKey()
            ?? throw new ArgumentException("The certificate authority holds no ECDSA private key.", nameof(authority));
        CertificateRequest request = CreateRequest(parameters, key, AuthorityKeyIdentifier(authority));
        DateTimeOffset now = timeProvider.GetUtcNow();
        using X509Certificate2 created = request.Create(
            authority.SubjectName, X509SignatureGenerator.CreateForECDsa(authorityKey), now, now.AddDays(CertificateDays), NewSerialNumber());
        string path = Path.Combine(outputFolder, prefix);
        string keyPem = key.ExportPkcs8PrivateKeyPem() + "\n";
        string certificatePem = created.ExportCertificatePem() + "\n";
        File.WriteAllText(path + ".key", keyPem);
        File.WriteAllText(path + ".pub.pem", key.ExportSubjectPublicKeyInfoPem() + "\n");
        File.WriteAllBytes(path + ".pub.der", key.ExportSubjectPublicKeyInfo());
        File.WriteAllText(path + ".crt", certificatePem);
        File.WriteAllBytes(path + ".der", created.RawData);
        File.WriteAllText(path + ".crl", RevocationListPem(created, authority, now));
        File.WriteAllText(path + ".pem", parametersText + keyPem + certificatePem);
    }

    private static byte[] AuthorityKeyIdentifier(X509Certificate2 authority)
    {
        foreach (X509Extension extension in authority.Extensions)
        {
            if (extension is X509SubjectKeyIdentifierExtension subjectKeyIdentifier)
            {
                return subjectKeyIdentifier.SubjectKeyIdentifierBytes.ToArray();
            }
        }

        throw new ArgumentException("The certificate authority has no subject key identifier.", nameof(authority));
    }

    private static string RevocationListPem(X509Certificate2 revoked, X509Certificate2 authority, DateTimeOffset now)
    {
        CertificateRevocationListBuilder builder = new();
        builder.AddEntry(revoked, now);
        byte[] revocationList = builder.Build(authority, BigInteger.One, now.AddDays(CertificateDays), HashAlgorithmName.SHA256, thisUpdate: now);
        return PemEncoding.WriteString("X509 CRL", revocationList) + "\n";
    }

    private static byte[] NewSerialNumber()
    {
        byte[] serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F;
        serial[0] |= 0x01;
        return serial;
    }

    // A null issuer key identifier means the request is self-signed: the authority key identifier
    // is then the subject's own, as keyid:always gives it for the CA.
    private static CertificateRequest CreateRequest(UpstreamCertificateParameters parameters, ECDsa key, byte[]? issuerKeyIdentifier)
    {
        CertificateRequest request = new(SubjectName(parameters), key, HashAlgorithmName.SHA256);
        X509SubjectKeyIdentifierExtension subjectKeyIdentifier = new(request.PublicKey, false);
        byte[] authorityKeyIdentifier = issuerKeyIdentifier ?? subjectKeyIdentifier.SubjectKeyIdentifierBytes.ToArray();
        foreach (KeyValuePair<string, string> pair in parameters.Section(ExtensionsSection(parameters)))
        {
            request.CertificateExtensions.Add(Extension(parameters, pair.Key, pair.Value, subjectKeyIdentifier, authorityKeyIdentifier));
        }

        return request;
    }

    private static string ExtensionsSection(UpstreamCertificateParameters parameters) =>
        parameters.Find(string.Empty, "extensions") ?? throw new FormatException("The .prm names no extensions section.");

    private static X500DistinguishedName SubjectName(UpstreamCertificateParameters parameters)
    {
        X500DistinguishedNameBuilder builder = new();
        builder.AddCountryOrRegion(DistinguishedNameValue(parameters, "countryName"));
        builder.AddOrganizationName(DistinguishedNameValue(parameters, "organizationName"));
        builder.AddCommonName(DistinguishedNameValue(parameters, "commonName"));
        return builder.Build();
    }

    private static string DistinguishedNameValue(UpstreamCertificateParameters parameters, string field)
    {
        string section = parameters.Find("req", "distinguished_name") ?? "req_DN";
        return parameters.Find(section, field + "_value") ?? throw new FormatException($"The .prm gives no {field}_value.");
    }

    // The x509v3 keys this generator writes, each from its value with any "critical," taken off.
    private static readonly Dictionary<string, Func<ExtensionValue, X509Extension>> ExtensionWriters = new(StringComparer.Ordinal)
    {
        ["basicConstraints"] = value => new X509BasicConstraintsExtension(
            string.Equals(value.Body, "CA:true", StringComparison.OrdinalIgnoreCase), false, 0, value.Critical),
        ["keyUsage"] = value => new X509KeyUsageExtension(KeyUsages(value.Body), value.Critical),
        ["extendedKeyUsage"] = value => new X509EnhancedKeyUsageExtension(ExtendedKeyUsages(value.Body), value.Critical),
        ["subjectAltName"] = value => SubjectAlternativeName(value.Body, value.Critical),
        ["subjectKeyIdentifier"] = value => value.SubjectKeyIdentifier,
        ["authorityKeyIdentifier"] = value => X509AuthorityKeyIdentifierExtension.CreateFromSubjectKeyIdentifier(value.AuthorityKeyIdentifier),
        ["authorityInfoAccess"] = value => new X509AuthorityInformationAccessExtension(null, Uris(value.Parameters, value.Body), value.Critical),
        ["crlDistributionPoints"] = value => CertificateRevocationListBuilder.BuildCrlDistributionPointExtension(Uris(value.Parameters, value.Body), value.Critical),
    };

    private static readonly Dictionary<string, X509KeyUsageFlags> KeyUsageFlags = new(StringComparer.Ordinal)
    {
        ["digitalSignature"] = X509KeyUsageFlags.DigitalSignature,
        ["keyEncipherment"] = X509KeyUsageFlags.KeyEncipherment,
        ["keyAgreement"] = X509KeyUsageFlags.KeyAgreement,
        ["keyCertSign"] = X509KeyUsageFlags.KeyCertSign,
        ["cRLSign"] = X509KeyUsageFlags.CrlSign,
    };

    private static readonly Dictionary<string, string> ExtendedKeyUsageOids = new(StringComparer.Ordinal)
    {
        ["serverAuth"] = ServerAuthOid,
        ["clientAuth"] = ClientAuthOid,
    };

    private static X509Extension Extension(
        UpstreamCertificateParameters parameters, string name, string value, X509SubjectKeyIdentifierExtension subjectKeyIdentifier, byte[] authorityKeyIdentifier)
    {
        if (!ExtensionWriters.TryGetValue(name, out Func<ExtensionValue, X509Extension>? write))
        {
            throw new FormatException($"The .prm names an extension this generator does not write: {name}");
        }

        bool critical = value.StartsWith("critical,", StringComparison.Ordinal);
        string body = critical ? value["critical,".Length..] : value;
        return write(new(parameters, body, critical, subjectKeyIdentifier, authorityKeyIdentifier));
    }

    private static X509KeyUsageFlags KeyUsages(string list)
    {
        X509KeyUsageFlags flags = X509KeyUsageFlags.None;
        foreach (string usage in list.Split(','))
        {
            flags |= KeyUsageFlags.TryGetValue(usage.Trim(), out X509KeyUsageFlags flag)
                ? flag
                : throw new FormatException($"The .prm names a key usage this generator does not write: {usage.Trim()}");
        }

        return flags;
    }

    private static OidCollection ExtendedKeyUsages(string list)
    {
        OidCollection oids = [];
        foreach (string usage in list.Split(','))
        {
            oids.Add(new Oid(ExtendedKeyUsageOids.TryGetValue(usage.Trim(), out string? oid)
                ? oid
                : throw new FormatException($"The .prm names an extended key usage this generator does not write: {usage.Trim()}")));
        }

        return oids;
    }

    // DNS:name entries, or OpenSSL's DER:hex:bytes form, which test-localhost0h.prm uses to put a
    // NUL inside a DNS name.
    private static X509Extension SubjectAlternativeName(string list, bool critical)
    {
        if (list.StartsWith("DER:", StringComparison.Ordinal))
        {
            return new X509Extension(SubjectAlternativeNameOid, Convert.FromHexString(list["DER:".Length..].Replace(":", string.Empty, StringComparison.Ordinal)), critical);
        }

        SubjectAlternativeNameBuilder builder = new();
        foreach (string entry in list.Split(','))
        {
            string trimmed = entry.Trim();
            if (!trimmed.StartsWith("DNS:", StringComparison.Ordinal))
            {
                throw new FormatException($"The .prm names a subject alternative name this generator does not write: {trimmed}");
            }

            builder.AddDnsName(trimmed["DNS:".Length..]);
        }

        return builder.Build(critical);
    }

    // The URIs of the @section a value names: keys URI.n or caIssuers;URI.n.
    private static string[] Uris(UpstreamCertificateParameters parameters, string reference)
    {
        if (!reference.StartsWith('@'))
        {
            throw new FormatException($"The .prm gives a URI list this generator does not read: {reference}");
        }

        List<string> uris = [];
        foreach (KeyValuePair<string, string> pair in parameters.Section(reference[1..]))
        {
            uris.Add(pair.Value);
        }

        return [.. uris];
    }

    private readonly record struct ExtensionValue(
        UpstreamCertificateParameters Parameters, string Body, bool Critical, X509SubjectKeyIdentifierExtension SubjectKeyIdentifier, byte[] AuthorityKeyIdentifier);
}
