using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// STD-180 (BL-5830): a <c>--crlfile</c> curl cannot load is exit 82, and the OpenSSL build
/// still reports <c>%{ssl_verify_result}</c> 1 for it.
/// </summary>
[TestClass]
public sealed class SslStreamTlsProviderUnusableCrlFileTests
{
    [TestMethod]
    public void ReportUnusableCrlFile_InTheOpenSslBuildWithExit82_ReportsVerifyResultOne()
    {
        var events = new RecordingTransferEvents();

        SslStreamTlsProvider.ReportUnusableCrlFile(events, CurlExitCode.SslCrlBadfile, isProxy: false, matchesSchannelBuild: false);

        Assert.HasCount(1, events.VerifyResults);
        Assert.AreEqual((1L, false), events.VerifyResults[0]);
    }

    [TestMethod]
    public void ReportUnusableCrlFile_InTheSchannelBuild_ReportsNothing()
    {
        var events = new RecordingTransferEvents();

        SslStreamTlsProvider.ReportUnusableCrlFile(events, CurlExitCode.SslCrlBadfile, isProxy: false, matchesSchannelBuild: true);

        Assert.IsEmpty(events.VerifyResults);
    }

    [TestMethod]
    public void ReportUnusableCrlFile_WithAnotherExitCode_ReportsNothing()
    {
        var events = new RecordingTransferEvents();

        SslStreamTlsProvider.ReportUnusableCrlFile(events, CurlExitCode.SslCacertBadfile, isProxy: false, matchesSchannelBuild: false);

        Assert.IsEmpty(events.VerifyResults);
    }
}
