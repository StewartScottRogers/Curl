using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;
using Curl.Testing;
using Curl.Tls;

using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins what the hand-built TLS client's verifier reports when .NET on Linux throws from its chain
/// build for OpenSSL's error 1 (a name constraint that does not decode, ADR-1446, BL-6062): exit 60
/// with verify result 1, as the OpenSSL build of curl answers it. The build step is replaced by one
/// that throws, since Windows does not throw; the <c>SslStream</c> path is pinned in
/// <c>SslStreamTlsProviderTests.UnspecifiedChain</c>.
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationUnspecifiedChainErrorTests
{
    private const string Host = "unspecified.example";

    private const string UnspecifiedBuildMessage = "OpenSSL mapping function MapOpenSsl30Code reported unhandled error code '1'.";

    private const string UnspecifiedRefusal = "SSL certificate OpenSSL verify result: unspecified certificate verification error (1)";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Verify_ChainBuildThrowingForOpenSslError1InTheOpenSslBuild_IsExit60WithVerifyResult1()
    {
        using var leaf = SelfSignedLeaf();
        var verifier = Verifier(() => throw new CryptographicException(UnspecifiedBuildMessage));

        var verdict = verifier.Verify(new ServerCertificateChain([leaf.RawData], Host, null));

        Diagnostics.Act("rejection", verdict.Rejection);
        Assert.IsFalse(verdict.IsAccepted);
        var (exitCode, message) = ((CurlExitCode, string))verdict.Rejection!;
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, exitCode);
        Assert.AreEqual(UnspecifiedRefusal, message);
        Assert.IsFalse(verifier.Observed.Verified);
        Assert.AreEqual(1L, verifier.Observed.VerifyResult);
    }

    [TestMethod]
    public void Verify_ChainBuildThrowingForAnotherOpenSslError_LetsTheExceptionThrough()
    {
        using var leaf = SelfSignedLeaf();
        var verifier = Verifier(() => throw new CryptographicException("OpenSSL mapping function MapOpenSsl30Code reported unhandled error code '12'."));

        Assert.ThrowsExactly<CryptographicException>(() => verifier.Verify(new ServerCertificateChain([leaf.RawData], Host, null)));
    }

    private static HandBuiltCertificateVerifier Verifier(Func<bool> build)
    {
        var verification = new ServerCertificateVerification(new TlsClientOptions(), false, TimeProvider.System);
        var anchors = verification.ReadTrustAnchors();
        return new HandBuiltCertificateVerifier(verification, anchors.ChainPolicy, anchors.AnchorsBesideSystemStore, anchors.RevocationLists, Host)
        {
            ChainBuildStep = (_, _) => build(),
        };
    }

    private static X509Certificate2 SelfSignedLeaf()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={Host}", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(Host);
        request.CertificateExtensions.Add(names.Build());
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }
}
