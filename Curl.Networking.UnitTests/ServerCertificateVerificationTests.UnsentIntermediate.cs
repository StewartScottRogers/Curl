using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that the OpenSSL build refuses a chain completed with an intermediate the server did not
/// send, as .NET's Unix chain completes it from the CurrentUser CA store (BL-5449, STD-131).
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationUnsentIntermediateTests
{
    private const string UnableToGetLocalIssuer = "SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)";

    [TestMethod]
    public void Judge_IntermediateTheServerDidNotSendInTheOpenSslBuild_IsExit60WithError20()
    {
        var (observed, failure) = JudgeSending(sendIntermediate: false, matchesSchannelBuild: false, insecure: false);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual(UnableToGetLocalIssuer, failure?.Message);
        Assert.IsFalse(observed.Verified);
        Assert.AreEqual(OpenSslVerifyResult.UnableToGetIssuerCertificateLocally, observed.VerifyResult);
    }

    [TestMethod]
    public void Judge_IntermediateTheServerSentInTheOpenSslBuild_Accepts()
    {
        var (_, failure) = JudgeSending(sendIntermediate: true, matchesSchannelBuild: false, insecure: false);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void Judge_IntermediateTheServerDidNotSendInTheSchannelBuild_Accepts()
    {
        var (_, failure) = JudgeSending(sendIntermediate: false, matchesSchannelBuild: true, insecure: false);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void Judge_IntermediateTheServerDidNotSendUnderInsecure_Accepts()
    {
        var (_, failure) = JudgeSending(sendIntermediate: false, matchesSchannelBuild: false, insecure: true);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void Judge_UnsentIntermediateTheAnchorsListInTheOpenSslBuild_Accepts()
    {
        var (_, failure) = JudgeSending(sendIntermediate: false, matchesSchannelBuild: false, insecure: false, anchorsListIntermediate: true);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void Judge_NoPeerCertificatesCapturedInTheOpenSslBuild_DoesNotRefuseAsAnUnsentIntermediate()
    {
        var (_, failure) = JudgeSending(sendIntermediate: false, matchesSchannelBuild: false, insecure: false, sendNothing: true);

        Assert.IsNull(failure);
    }

    private static (PeerVerification Observed, (CurlExitCode ExitCode, string Message)? Failure) JudgeSending(
        bool sendIntermediate, bool matchesSchannelBuild, bool insecure, bool anchorsListIntermediate = false, bool sendNothing = false)
    {
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Unsent Root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        using var intermediateKey = RSA.Create(2048);
        var intermediateRequest = new CertificateRequest("CN=Unsent Intermediate", intermediateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var intermediatePublic = intermediateRequest.Create(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(20), [1]);
        using var intermediate = intermediatePublic.CopyWithPrivateKey(intermediateKey);

        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=unsent.test", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var leaf = leafRequest.Create(intermediate, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10), [2]);

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.ExtraStore.Add(intermediate);
        if (anchorsListIntermediate)
        {
            chain.ChainPolicy.CustomTrustStore.Add(intermediate);
        }
        Assert.IsTrue(chain.Build(leaf));
        Assert.AreEqual(3, chain.ChainElements.Count);

        ReadOnlyMemory<byte>[] sent = sendNothing ? [] : sendIntermediate ? [leaf.RawData, intermediate.RawData] : [leaf.RawData];
        var verification = new ServerCertificateVerification(new TlsClientOptions(Insecure: insecure), matchesSchannelBuild, TimeProvider.System);

        return verification.Judge(SslPolicyErrors.None, chain, "unsent.test", [], null, sent);
    }
}
