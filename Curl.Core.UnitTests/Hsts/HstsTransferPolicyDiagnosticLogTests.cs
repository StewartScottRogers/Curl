using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Core.Hsts;

/// <summary>
/// Pins what <see cref="HstsTransferPolicy" /> writes to Curl's own diagnostic log, component
/// <c>hsts</c> (ADR-0222, BL-921): a URL switched to <c>https</c> as <c>info</c>, a header learned and
/// a host it does not know as <c>verbose</c>, naming the host and never a credential.
/// </summary>
[TestClass]
public sealed class HstsTransferPolicyDiagnosticLogTests
{
    private const string UnknownUrl = "http://127.0.0.1:18921/a";

    private static readonly DateTimeOffset Start = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void TrySwitchToHttps_HostTheCacheKnows_LogsInfoNamingTheHostButNotTheCredentials()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        HstsTransferPolicy hsts = new(new FakeTimeProvider(Start), log);
        hsts.StoreFromResponse(CurlUrl.Parse("https://example.com/"), "max-age=60", Start);
        const string url = "http://user:s3cret@example.com/x";

        bool switched = hsts.TrySwitchToHttps(url, CurlUrl.Parse(url), out _);

        Assert.IsTrue(switched);
        CollectionAssert.AreEqual(
            new[]
            {
                (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Hsts, "stored entry for example.com"),
                (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Hsts, "learned Strict-Transport-Security from example.com"),
                (DiagnosticLogLevel.Info, DiagnosticLogComponents.Hsts, "http URL to example.com switched to https by its HSTS entry"),
            },
            log.Lines);
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains("s3cret", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TrySwitchToHttps_UnknownHost_LogsTheMissAsVerbose()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        HstsTransferPolicy hsts = new(new FakeTimeProvider(Start), log);

        bool switched = hsts.TrySwitchToHttps(UnknownUrl, CurlUrl.Parse(UnknownUrl), out _);

        Assert.IsFalse(switched);
        CollectionAssert.AreEqual(new[] { "no HSTS entry for 127.0.0.1; http kept" }, log.At(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public void TrySwitchToHttps_AtLogLevelError_RecordsNoInfoOrVerboseLine()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        HstsTransferPolicy hsts = new(new FakeTimeProvider(Start), log);
        hsts.StoreFromResponse(CurlUrl.Parse("https://example.com/"), "max-age=60", Start);

        hsts.TrySwitchToHttps("http://example.com/", CurlUrl.Parse("http://example.com/"), out _);
        hsts.TrySwitchToHttps(UnknownUrl, CurlUrl.Parse(UnknownUrl), out _);

        Assert.IsEmpty(log.Lines);
    }
}
