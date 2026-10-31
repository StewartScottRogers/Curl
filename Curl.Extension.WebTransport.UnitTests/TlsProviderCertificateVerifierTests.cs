using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Extension.WebTransport.Session;

[TestClass]
public sealed class TlsProviderCertificateVerifierTests
{
    [TestMethod]
    public void Verify_ProviderAccepts_AcceptsAndHandsOverTheChainAndTargetHost()
    {
        var provider = new ChainJudgingTlsProvider(ServerCertificateChainVerdict.Accepted);
        var verifier = new TlsProviderCertificateVerifier(provider, "doq.example");

        ServerCertificateVerdict verdict = verifier.Verify(new ServerCertificateChain([[1, 2], [3]], "sni.example", null));

        Assert.IsTrue(verdict.IsAccepted);
        Assert.AreEqual("doq.example", provider.TargetHost);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, provider.Certificates![0].ToArray());
        CollectionAssert.AreEqual(new byte[] { 3 }, provider.Certificates[1].ToArray());
    }

    [TestMethod]
    public void Verify_ProviderRejects_RejectsWithTheProvidersMessage()
    {
        var provider = new ChainJudgingTlsProvider(ServerCertificateChainVerdict.Rejected(CurlExitCode.PeerFailedVerification, "untrusted root"));

        ServerCertificateVerdict verdict = new TlsProviderCertificateVerifier(provider, "doq.example").Verify(new ServerCertificateChain([[1]], null, null));

        Assert.IsFalse(verdict.IsAccepted);
        Assert.AreEqual("untrusted root", verdict.Rejection);
    }

    [TestMethod]
    public void Verify_NullChain_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new TlsProviderCertificateVerifier(new ChainJudgingTlsProvider(ServerCertificateChainVerdict.Accepted), "doq.example").Verify(null!));

    [TestMethod]
    public void CertificateVerifierFactory_MakesAVerifierForTheHostOverTheProvider()
    {
        var provider = new ChainJudgingTlsProvider(ServerCertificateChainVerdict.Accepted);

        TlsProviderCertificateVerifier verifier = WebTransportProtocolHandler.CertificateVerifierFactory(provider)("wt.example");
        verifier.Verify(new ServerCertificateChain([[1]], null, null));

        Assert.AreEqual("wt.example", provider.TargetHost);
    }

    private sealed class ChainJudgingTlsProvider(ServerCertificateChainVerdict verdict) : ITlsProvider
    {
        public IReadOnlyList<ReadOnlyMemory<byte>>? Certificates { get; private set; }

        public string? TargetHost { get; private set; }

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ServerCertificateChainVerdict VerifyServerCertificateChain(IReadOnlyList<ReadOnlyMemory<byte>> certificates, string targetHost)
        {
            Certificates = certificates;
            TargetHost = targetHost;
            return verdict;
        }
    }
}
