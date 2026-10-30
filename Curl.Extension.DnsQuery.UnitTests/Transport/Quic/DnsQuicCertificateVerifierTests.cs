using Curl.Extension.DnsQuery.Transport.Quic;
using Curl.Extension.DnsQuery.UnitTests.Transport.Tls;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Extension.DnsQuery.UnitTests.Transport.Quic;

/// <summary>Checks that the DoQ server's certificate chain is judged by curl's TLS provider (RFC 9250 section 10.1).</summary>
[TestClass]
public sealed class DnsQuicCertificateVerifierTests
{
    [TestMethod]
    public void Verify_ProviderAccepts_AcceptsAndHandsOverTheChainAndTargetHost()
    {
        var provider = new HandshakingTlsProvider();

        ServerCertificateVerdict verdict = new DnsQuicCertificateVerifier(provider, "doq.example").Verify(new ServerCertificateChain([[1, 2], [3]], "sni.example", null));

        Assert.IsTrue(verdict.IsAccepted);
        Assert.AreEqual("doq.example", provider.TargetHost);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, provider.Certificates![0].ToArray());
        CollectionAssert.AreEqual(new byte[] { 3 }, provider.Certificates[1].ToArray());
    }

    [TestMethod]
    public void Verify_ProviderRejects_RejectsWithTheProvidersMessage()
    {
        var provider = new HandshakingTlsProvider { ChainVerdict = ServerCertificateChainVerdict.Rejected(CurlExitCode.PeerFailedVerification, "untrusted root") };

        ServerCertificateVerdict verdict = new DnsQuicCertificateVerifier(provider, "doq.example").Verify(new ServerCertificateChain([[1]], null, null));

        Assert.IsFalse(verdict.IsAccepted);
        Assert.AreEqual("untrusted root", verdict.Rejection);
    }

    [TestMethod]
    public void Verify_NullChain_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsQuicCertificateVerifier(new HandshakingTlsProvider(), "doq.example").Verify(null!));
}
