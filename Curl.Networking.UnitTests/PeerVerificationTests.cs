using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// Pins what <see cref="PeerVerification.ReportVerifyResult" /> reports for
/// <c>%{ssl_verify_result}</c> and <c>%{proxy_ssl_verify_result}</c> (BL-661).
/// </summary>
[TestClass]
public sealed class PeerVerificationTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReportVerifyResult_OpenSslBuild_ReportsTheCodeWithWhetherItIsTheProxys(bool isProxy)
    {
        var events = new RecordingTransferEvents();

        new PeerVerification(false, 20, []).ReportVerifyResult(events, isProxy, matchesSchannelBuild: false);

        CollectionAssert.AreEqual(new[] { (20L, isProxy) }, events.VerifyResults);
    }

    [TestMethod]
    public void ReportVerifyResult_CodeWithNoKnownMapping_ReportsUnspecified()
    {
        var events = new RecordingTransferEvents();

        new PeerVerification(false, null, []).ReportVerifyResult(events, isProxy: false, matchesSchannelBuild: false);

        CollectionAssert.AreEqual(new[] { (OpenSslVerifyResult.Unspecified, false) }, events.VerifyResults);
    }

    [TestMethod]
    public void ReportVerifyResult_SchannelBuild_ReportsNothing()
    {
        var events = new RecordingTransferEvents();

        new PeerVerification(false, 18, []).ReportVerifyResult(events, isProxy: false, matchesSchannelBuild: true);

        Assert.IsEmpty(events.VerifyResults);
    }

    [TestMethod]
    public void ReportVerifyResult_Unobserved_ReportsNothing()
    {
        var events = new RecordingTransferEvents();

        PeerVerification.Unobserved.ReportVerifyResult(events, isProxy: false, matchesSchannelBuild: false);

        Assert.IsEmpty(events.VerifyResults);
    }
}
