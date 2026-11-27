using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the chain build .NET on Linux cannot finish (ADR-1446): its exception for OpenSSL's error 1
/// is a refused chain with verify result 1. The exception is thrown here by hand, so every platform runs it.
/// </summary>
[TestClass]
public sealed class CertificateChainBuildTests
{
    private const string UnspecifiedErrorMessage = "OpenSSL mapping function MapOpenSsl30Code reported unhandled error code '1'.";

    private const string UnspecifiedRefusal = "SSL certificate OpenSSL verify result: unspecified certificate verification error (1)";

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Build_BuildThatReturns_AnswersItsResultAndIsNotUnspecified(bool verified)
    {
        using var chain = new X509Chain();

        var built = CertificateChainBuild.Build(chain, () => verified);

        Assert.AreEqual(verified, built);
        Assert.IsFalse(CertificateChainBuild.EndedAsUnspecified(chain));
    }

    [TestMethod]
    public void Build_BuildEndingWithOpenSslErrorOne_IsFalseAndEndsAsUnspecified()
    {
        using var chain = new X509Chain();

        var built = CertificateChainBuild.Build(chain, () => throw new CryptographicException(UnspecifiedErrorMessage));

        Assert.IsFalse(built);
        Assert.IsTrue(CertificateChainBuild.EndedAsUnspecified(chain));
    }

    [TestMethod]
    public void Build_AnotherCryptographicFailure_Throws()
    {
        using var chain = new X509Chain();

        Assert.ThrowsExactly<CryptographicException>(() => CertificateChainBuild.Build(
            chain, () => throw new CryptographicException("OpenSSL mapping function MapOpenSsl30Code reported unhandled error code '12'.")));

        Assert.IsFalse(CertificateChainBuild.EndedAsUnspecified(chain));
    }

    [TestMethod]
    public void EndedAsUnspecified_NoChain_IsFalse() => Assert.IsFalse(CertificateChainBuild.EndedAsUnspecified(null));

    [TestMethod]
    [DataRow(SslPolicyErrors.RemoteCertificateChainErrors)]
    [DataRow(SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch)]
    public void VerifyPeer_OpenSslBuildChainEndedAsUnspecified_IsExit60WithVerifyResultOne(SslPolicyErrors errors)
    {
        using var chain = UnspecifiedChain();

        var failure = OpenSslVerification().VerifyPeer(errors, chain, "unspecified.test", []);

        Assert.AreEqual((CurlExitCode.PeerFailedVerification, UnspecifiedRefusal), failure);
    }

    [TestMethod]
    public void Judge_OpenSslBuildChainEndedAsUnspecified_ObservesVerifyResultOne()
    {
        using var chain = UnspecifiedChain();

        var (observed, failure) = OpenSslVerification().Judge(
            SslPolicyErrors.RemoteCertificateChainErrors, chain, "unspecified.test", [], null, [new byte[] { 1 }]);

        Assert.AreEqual(1L, observed.VerifyResult);
        Assert.AreEqual((CurlExitCode.PeerFailedVerification, UnspecifiedRefusal), failure);
    }

    private static ServerCertificateVerification OpenSslVerification() =>
        new(new TlsClientOptions(), matchesSchannelBuild: false, TimeProvider.System);

    private static X509Chain UnspecifiedChain()
    {
        var chain = new X509Chain();
        CertificateChainBuild.Build(chain, () => throw new CryptographicException(UnspecifiedErrorMessage));
        return chain;
    }
}
