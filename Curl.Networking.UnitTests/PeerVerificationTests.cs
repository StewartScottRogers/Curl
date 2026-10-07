using Curl.Networking.Fakes;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins what <see cref="PeerVerification.ReportVerifyResult" /> reports for
/// <c>%{ssl_verify_result}</c> and <c>%{proxy_ssl_verify_result}</c> (BL-661).
/// </summary>
[TestClass]
public sealed class PeerVerificationTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReportVerifyResult_OpenSslBuild_ReportsTheCodeWithWhetherItIsTheProxys(bool isProxy)
    {
        var events = new RecordingTransferEvents();

        Diagnostics.Arrange("verify result, proxy, Schannel build", $"20, {isProxy}, False");

        new PeerVerification(false, 20, []).ReportVerifyResult(events, isProxy, matchesSchannelBuild: false);

        Diagnostics.Act("verify results", string.Join(", ", events.VerifyResults));
        Diagnostics.Assert("verify results", $"(20, {isProxy})", string.Join(", ", events.VerifyResults));

        CollectionAssert.AreEqual(new[] { (20L, isProxy) }, events.VerifyResults);
    }

    [TestMethod]
    public void ReportVerifyResult_CodeWithNoKnownMapping_ReportsUnspecified()
    {
        var events = new RecordingTransferEvents();

        Diagnostics.Arrange("verify result, proxy, Schannel build", "none, False, False");

        new PeerVerification(false, null, []).ReportVerifyResult(events, isProxy: false, matchesSchannelBuild: false);

        Diagnostics.Act("verify results", string.Join(", ", events.VerifyResults));
        Diagnostics.Assert("verify results", $"({OpenSslVerifyResult.Unspecified}, False)", string.Join(", ", events.VerifyResults));

        CollectionAssert.AreEqual(new[] { (OpenSslVerifyResult.Unspecified, false) }, events.VerifyResults);
    }

    [TestMethod]
    public void ReportVerifyResult_SchannelBuild_ReportsNothing()
    {
        var events = new RecordingTransferEvents();

        Diagnostics.Arrange("verify result, proxy, Schannel build", "18, False, True");

        new PeerVerification(false, 18, []).ReportVerifyResult(events, isProxy: false, matchesSchannelBuild: true);

        Diagnostics.Act("verify results", events.VerifyResults.Count);
        Diagnostics.Assert("verify results", 0, events.VerifyResults.Count);

        Assert.IsEmpty(events.VerifyResults);
    }

    [TestMethod]
    public void ReportVerifyResult_Unobserved_ReportsNothing()
    {
        var events = new RecordingTransferEvents();

        Diagnostics.Arrange("peer verification", "unobserved, OpenSSL build");

        PeerVerification.Unobserved.ReportVerifyResult(events, isProxy: false, matchesSchannelBuild: false);

        Diagnostics.Act("verify results", events.VerifyResults.Count);
        Diagnostics.Assert("verify results", 0, events.VerifyResults.Count);

        Assert.IsEmpty(events.VerifyResults);
    }

    [TestMethod]
    public void ReportPinnedPublicKeyRefusal_NotRefused_ReportsNothing()
    {
        var events = new RecordingTransferEvents();

        Diagnostics.Arrange("pinned public key", "sha256//AAAA, not refused, Schannel build");

        new PeerVerification(true, 0, []) { PinnedPublicKeyHash = "sha256//AAAA" }.ReportPinnedPublicKeyRefusal(events, matchesSchannelBuild: true);

        Diagnostics.Act("info lines", events.Info.Count);
        Diagnostics.Assert("info lines", 0, events.Info.Count);

        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public void ReportPinnedPublicKeyRefusal_FilePinRefused_ReportsOnlyTheMismatchLines()
    {
        var events = new RecordingTransferEvents();

        Diagnostics.Arrange("pinned public key", "file pin, refused, Schannel build");

        new PeerVerification(true, 0, []) { PinnedPublicKeyRefused = true }.ReportPinnedPublicKeyRefusal(events, matchesSchannelBuild: true);

        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Assert("info line count", 2, events.Info.Count);

        CollectionAssert.AreEqual(
            new[] { "SSL: public key does not match pinned public key", "SSL: public key does not match pinned public key" },
            events.Info);
    }

    [TestMethod]
    public void ReportPinnedPublicKeyRefusal_HashPinRefusedInTheOpenSslBuild_ReportsOnlyTheMismatchLine()
    {
        // The failed handshake's event prints the " public key hash:" line (BL-1149).
        var events = new RecordingTransferEvents();

        new PeerVerification(true, 0, []) { PinnedPublicKeyHash = "sha256//AAAA", PinnedPublicKeyRefused = true }
            .ReportPinnedPublicKeyRefusal(events, matchesSchannelBuild: false);

        Diagnostics.Arrange("pinned public key", "sha256//AAAA, refused, OpenSSL build");
        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Assert("info line count", 1, events.Info.Count);

        CollectionAssert.AreEqual(new[] { "SSL: public key does not match pinned public key" }, events.Info);
    }
}
