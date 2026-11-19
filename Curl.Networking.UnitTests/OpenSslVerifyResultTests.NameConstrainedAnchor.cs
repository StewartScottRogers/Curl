using System.Formats.Asn1;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Pins that a chain anchored at a listed name-constrained intermediate, which .NET finds only
/// partial, reports OpenSSL's 47 or 48 and not 20 (STD-101).
/// </summary>
[TestClass]
public sealed class OpenSslVerifyResultNameConstrainedAnchorTests
{
    [TestMethod]
    public void Of_LeafOutsideTheListedIntermediatesPermittedSubtree_Is47()
    {
        using var chain = BuildChainAnchoredAtIntermediate(permitted: "example.com", excluded: null, listAnchor: true);

        Assert.AreEqual(OpenSslVerifyResult.PermittedSubtreeViolation, OpenSslVerifyResult.Of(SslPolicyErrors.RemoteCertificateChainErrors, chain, DateTimeOffset.UtcNow));
        Assert.AreEqual(
            "SSL certificate OpenSSL verify result: permitted subtree violation (47)",
            TlsFailureMessages.OpenSslPeerFailedVerification(SslPolicyErrors.RemoteCertificateChainErrors, chain, "localhost"));
    }

    [TestMethod]
    public void Of_LeafInsideTheListedIntermediatesExcludedSubtree_Is48()
    {
        using var chain = BuildChainAnchoredAtIntermediate(permitted: null, excluded: "localhost", listAnchor: true);

        Assert.AreEqual(OpenSslVerifyResult.ExcludedSubtreeViolation, OpenSslVerifyResult.Of(SslPolicyErrors.RemoteCertificateChainErrors, chain, DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void Of_NothingBrokenBelowTheListedIntermediate_StaysUnableToGetIssuer()
    {
        using var chain = BuildChainAnchoredAtIntermediate(permitted: "localhost", excluded: null, listAnchor: true);

        Assert.AreEqual(OpenSslVerifyResult.UnableToGetIssuerCertificateLocally, OpenSslVerifyResult.Of(SslPolicyErrors.RemoteCertificateChainErrors, chain, DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void Of_IntermediateNotListed_StaysUnableToGetIssuer()
    {
        using var chain = BuildChainAnchoredAtIntermediate(permitted: "example.com", excluded: null, listAnchor: false);

        Assert.AreEqual(OpenSslVerifyResult.UnableToGetIssuerCertificateLocally, OpenSslVerifyResult.Of(SslPolicyErrors.RemoteCertificateChainErrors, chain, DateTimeOffset.UtcNow));
    }

    private static X509Chain BuildChainAnchoredAtIntermediate(string? permitted, string? excluded, bool listAnchor)
    {
        using var root = Authority("CN=Anchor Root", null, null);
        using var intermediate = Authority("CN=Anchor Intermediate", root, Constraints(permitted, excluded));
        using var leaf = Leaf(intermediate);
        var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        using var unrelated = Authority("CN=Unrelated Root", null, null);
        chain.ChainPolicy.CustomTrustStore.Add(X509CertificateLoader.LoadCertificate((listAnchor ? intermediate : unrelated).RawData));
        chain.ChainPolicy.ExtraStore.Add(X509CertificateLoader.LoadCertificate(intermediate.RawData));
        chain.Build(leaf);
        return chain;
    }

    private static byte[] Constraints(string? permitted, string? excluded)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            WriteSubtrees(writer, 0, permitted);
            WriteSubtrees(writer, 1, excluded);
        }

        return writer.Encode();
    }

    private static void WriteSubtrees(AsnWriter writer, int tag, string? dnsName)
    {
        if (dnsName is null)
        {
            return;
        }

        using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, tag, true)))
        using (writer.PushSequence())
        {
            writer.WriteCharacterString(UniversalTagNumber.IA5String, dnsName, new Asn1Tag(TagClass.ContextSpecific, 2));
        }
    }

    private static X509Certificate2 Authority(string subject, X509Certificate2? issuer, byte[]? nameConstraints)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        if (nameConstraints is not null)
        {
            request.CertificateExtensions.Add(new X509Extension(new Oid("2.5.29.30"), nameConstraints, true));
        }

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        using var certificate = issuer is null
            ? request.CreateSelfSigned(notBefore, notBefore.AddDays(60))
            : request.Create(issuer, notBefore, notBefore.AddDays(30), RandomNumberGenerator.GetBytes(8)).CopyWithPrivateKey(key);
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);
    }

    private static X509Certificate2 Leaf(X509Certificate2 issuer)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.Create(issuer, issuer.NotBefore.AddHours(1), issuer.NotAfter.AddDays(-10), RandomNumberGenerator.GetBytes(8));
        return X509CertificateLoader.LoadCertificate(certificate.RawData);
    }
}
