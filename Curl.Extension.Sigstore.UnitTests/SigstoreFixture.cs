using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Curl.Extension.Sigstore.UnitTests;

/// <summary>
/// A stand-in Fulcio: a self-signed CA, and signing certificates it issues as Fulcio's certificate specification
/// describes them (an e-mail or URI subject alternative name, the issuer extensions, the code-signing usage, ten
/// minutes of validity), with Sigstore bundles over an artifact signed by their keys.
/// </summary>
internal sealed class SigstoreFixture : IDisposable
{
    public const string Email = "signer@example.com";
    public const string Workflow = "https://github.com/example/project/.github/workflows/release.yml@refs/heads/main";
    public const string Issuer = "https://accounts.example.com";

    public static readonly DateTimeOffset IssuedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ECDsa rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public SigstoreFixture()
    {
        CertificateRequest request = new("CN=sigstore-test-root", rootKey, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        Root = request.CreateSelfSigned(IssuedAt.AddYears(-1), IssuedAt.AddYears(5));
    }

    public X509Certificate2 Root { get; }

    public static string RootPem(params X509Certificate2[] certificates) =>
        string.Concat(certificates.Select(certificate => certificate.ExportCertificatePem() + "\n"));

    /// <summary>Issues a signing certificate for <paramref name="key" />.</summary>
    public X509Certificate2 Issue(AsymmetricAlgorithm key, Action<SubjectAlternativeNameBuilder>? names = null, string? issuerV2 = Issuer, string? issuerV1 = null, bool withSubjectAlternativeName = true)
    {
        var request = key is ECDsa ecdsa
            ? new CertificateRequest("", ecdsa, HashAlgorithmName.SHA256)
            : new CertificateRequest("", (RSA)key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.3")], false));
        if (withSubjectAlternativeName)
        {
            SubjectAlternativeNameBuilder builder = new();
            (names ?? (b => b.AddEmailAddress(Email)))(builder);
            request.CertificateExtensions.Add(builder.Build(true));
        }

        if (issuerV2 is not null)
        {
            AsnWriter writer = new(AsnEncodingRules.DER);
            writer.WriteCharacterString(UniversalTagNumber.UTF8String, issuerV2);
            request.CertificateExtensions.Add(new X509Extension("1.3.6.1.4.1.57264.1.8", writer.Encode(), false));
        }

        if (issuerV1 is not null)
        {
            request.CertificateExtensions.Add(new X509Extension("1.3.6.1.4.1.57264.1.1", Encoding.UTF8.GetBytes(issuerV1), false));
        }

        return request.Create(Root.SubjectName, X509SignatureGenerator.CreateForECDsa(rootKey), IssuedAt, IssuedAt.AddMinutes(10), [1, 2, 3, 4]);
    }

    /// <summary>Writes a v0.3 bundle (or a v0.2 one with an <c>x509CertificateChain</c>) as cosign does.</summary>
    public static string Bundle(X509Certificate2 certificate, byte[] artifact, byte[] signature, string mediaVersion = "v0.3", string algorithm = "SHA2_256", string integratedTime = "\"1790856300\"")
    {
        var raw = Convert.ToBase64String(certificate.RawData);
        var material = mediaVersion == "v0.3"
            ? $"\"certificate\": {{ \"rawBytes\": \"{raw}\" }}"
            : $"\"x509CertificateChain\": {{ \"certificates\": [ {{ \"rawBytes\": \"{raw}\" }} ] }}";
        return $$"""
            {
              "mediaType": "application/vnd.dev.sigstore.bundle.{{mediaVersion}}+json",
              "verificationMaterial": {
                {{material}},
                "tlogEntries": [ { "logIndex": "1", "integratedTime": {{integratedTime}} } ]
              },
              "messageSignature": {
                "messageDigest": { "algorithm": "{{algorithm}}", "digest": "{{Convert.ToBase64String(SHA256.HashData(artifact))}}" },
                "signature": "{{Convert.ToBase64String(signature)}}"
              }
            }
            """;
    }

    public static byte[] Sign(ECDsa key, byte[] artifact) =>
        key.SignData(artifact, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

    public void Dispose()
    {
        Root.Dispose();
        rootKey.Dispose();
    }
}
