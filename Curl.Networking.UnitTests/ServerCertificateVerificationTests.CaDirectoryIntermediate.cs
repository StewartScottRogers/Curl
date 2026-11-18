using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins OpenSSL curl's partial-chain tolerance for a CA directory (<c>--capath</c>,
/// <c>--proxy-capath</c>, <c>SSL_CERT_DIR</c>) holding only an intermediate CA (BL-5439).
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationCaDirectoryIntermediateTests
{
    private const string Host = "capath-intermediate.test";

    private string _directory = null!;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "curl-capath-int-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public void VerifyPeer_CaDirectoryHoldingOnlyTheIntermediate_Accepts()
    {
        using var root = CreateCa("CN=Int Root", null, null);
        using var intermediate = CreateCa("CN=Int Intermediate", root, null);
        using var leaf = CreateLeaf(intermediate);

        Assert.IsNull(Verify(intermediate, leaf));
    }

    [TestMethod]
    public void VerifyPeer_CaDirectoryHoldingOnlyAnIntermediateThatExcludesTheName_IsExit60()
    {
        using var root = CreateCa("CN=Int Root", null, null);
        using var intermediate = CreateCa("CN=Int Intermediate", root, Host);
        using var leaf = CreateLeaf(intermediate);

        var failure = Verify(intermediate, leaf);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    private (CurlExitCode ExitCode, string Message)? Verify(X509Certificate2 anchor, X509Certificate2 leaf)
    {
        var directory = Path.Combine(_directory, "capath");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{OpenSslSubjectNameHash.Of(anchor.SubjectName)}.0"), anchor.ExportCertificatePem());
        var verification = new ServerCertificateVerification(new TlsClientOptions(CaCertificateDirectory: directory), false, TimeProvider.System);
        var (_, anchors, _) = verification.ReadTrustAnchors();
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.ExtraStore.Add(anchor);
        chain.Build(leaf);
        return verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, anchors);
    }

    private static X509Certificate2 CreateCa(string subject, X509Certificate2? issuer, string? excludedDns)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        if (excludedDns is not null)
        {
            request.CertificateExtensions.Add(ExcludedDnsConstraint(excludedDns));
        }

        var notBefore = DateTimeOffset.UtcNow.AddDays(-2);
        var notAfter = DateTimeOffset.UtcNow.AddDays(30);
        if (issuer is null)
        {
            return request.CreateSelfSigned(notBefore, notAfter);
        }

        using var certificate = request.Create(issuer, notBefore, notAfter, RandomNumberGenerator.GetBytes(8));
        return certificate.CopyWithPrivateKey(key);
    }

    // nameConstraints, critical: excludedSubtrees [1] { dNSName [2] <name> }.
    private static X509Extension ExcludedDnsConstraint(string name)
    {
        var writer = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        using (writer.PushSequence())
        using (writer.PushSequence(new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 1)))
        using (writer.PushSequence())
        {
            writer.WriteCharacterString(
                System.Formats.Asn1.UniversalTagNumber.IA5String,
                name,
                new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 2));
        }

        return new X509Extension(new Oid("2.5.29.30"), writer.Encode(), true);
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={Host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(Host);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        return request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10), RandomNumberGenerator.GetBytes(8));
    }
}
