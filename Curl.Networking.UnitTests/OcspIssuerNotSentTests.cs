using Curl.Networking.Fakes.Tls13Server;
using Curl.Testing;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// <c>--cert-status</c> with a trust store against a server that sends only its leaf: curl
/// 8.18.0 on OpenSSL 3.5.5 finds no issuer to compute the leaf's OCSP <c>CertID</c> from and
/// exits 91 <c>Error computing OCSP ID</c>, measured against <c>openssl s_server -status_file</c>
/// with no <c>-cert_chain</c> (BL-3696, BL-3917).
/// </summary>
[TestClass]
public sealed class OcspIssuerNotSentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void AGoodStapleFromAServerThatSendsNoIssuer_IsCurlsErrorComputingOcspId()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("chain", "the leaf only");
        Diagnostics.Arrange("response", "good, signed by the issuer");

        OcspStapleOutcome outcome = OcspStapleVerifier.Verify(pki.Response(Now).Build(), [pki.Leaf.RawData], Now);
        string message = CertificateStatusFailureMessages.For(outcome);

        Diagnostics.Assert("message", "Error computing OCSP ID", message);
        Assert.AreEqual("Error computing OCSP ID", message);
    }
}
