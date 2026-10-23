using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the OpenSSL build's partial-chain rule (BL-3335): curl sets <c>X509_V_FLAG_PARTIAL_CHAIN</c>,
/// so a <c>--cacert</c> file holding only the server's leaf or only an intermediate of its chain
/// verifies the server (measured with curl 8.18.0 on OpenSSL 3.5.5: exit 0 for the leaf, the
/// intermediate and the root alone). The Schannel build takes a listed leaf only.
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationPartialChainTests
{
    private const string Host = "partial-chain.test";

    private static readonly X509ChainStatus[] PartialChainStatus = [new() { Status = X509ChainStatusFlags.PartialChain }];

    [TestMethod]
    public void VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheOpenSslBuild_Accepts()
    {
        using var root = CreateCa("CN=Partial Root", null);
        using var intermediate = CreateCa("CN=Partial Intermediate", root);
        using var leaf = CreateLeaf(intermediate);

        var failure = Verify(matchesSchannelBuild: false, leaf, intermediate, listed: intermediate);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_CaCertificateFileHoldingOnlyTheLeafBehindAnIntermediateInTheOpenSslBuild_Accepts()
    {
        using var root = CreateCa("CN=Partial Root", null);
        using var intermediate = CreateCa("CN=Partial Intermediate", root);
        using var leaf = CreateLeaf(intermediate);

        var failure = Verify(matchesSchannelBuild: false, leaf, intermediate, listed: leaf);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_CaCertificateFileHoldingAnUnrelatedCertificateInTheOpenSslBuild_IsExit60()
    {
        using var root = CreateCa("CN=Partial Root", null);
        using var intermediate = CreateCa("CN=Partial Intermediate", root);
        using var leaf = CreateLeaf(intermediate);
        using var unrelated = CreateCa("CN=Unrelated", null);

        var failure = Verify(matchesSchannelBuild: false, leaf, intermediate, listed: unrelated);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheSchannelBuild_IsExit60()
    {
        using var root = CreateCa("CN=Partial Root", null);
        using var intermediate = CreateCa("CN=Partial Intermediate", root);
        using var leaf = CreateLeaf(intermediate);

        var failure = Verify(matchesSchannelBuild: true, leaf, intermediate, listed: intermediate);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    // Judges the chain .NET builds for the leaf with the listed certificate as the only
    // custom trust, its status standing in for the partial chain every platform reports.
    private static (CurlExitCode ExitCode, string Message)? Verify(
        bool matchesSchannelBuild, X509Certificate2 leaf, X509Certificate2 intermediate, X509Certificate2 listed)
    {
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateFile: "ca.pem", SkipRevocationCheck: true), matchesSchannelBuild, TimeProvider.System)
        {
            ReadChainStatus = _ => PartialChainStatus,
        };
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(listed);
        chain.ChainPolicy.ExtraStore.Add(intermediate);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.Build(leaf);
        return verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, []);
    }

    private static X509Certificate2 CreateCa(string subject, X509Certificate2? issuer)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        var notBefore = DateTimeOffset.UtcNow.AddDays(-2);
        var notAfter = DateTimeOffset.UtcNow.AddDays(30);
        if (issuer is null)
        {
            return request.CreateSelfSigned(notBefore, notAfter);
        }

        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        using var signed = request.Create(issuer, notBefore, notAfter, RandomNumberGenerator.GetBytes(8));
        return signed.CopyWithPrivateKey(key);
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={Host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(Host);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        return request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10), RandomNumberGenerator.GetBytes(8));
    }
}
