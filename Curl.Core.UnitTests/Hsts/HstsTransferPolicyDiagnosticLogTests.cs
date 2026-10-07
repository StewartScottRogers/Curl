using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TrySwitchToHttps_HostTheCacheKnows_LogsInfoNamingTheHostButNotTheCredentials()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        HstsTransferPolicy hsts = new(new FakeTimeProvider(Start), log);
        hsts.StoreFromResponse(CurlUrl.Parse("https://example.com/"), "max-age=60", Start);
        const string url = "http://user:s3cret@example.com/x";
        diagnostics.Arrange("stored header", "max-age=60 for example.com");
        diagnostics.Arrange("url", "http://user:***@example.com/x");

        bool switched = hsts.TrySwitchToHttps(url, CurlUrl.Parse(url), out _);

        diagnostics.Act("switched", switched);
        diagnostics.Assert("switched", true, switched);
        Assert.IsTrue(switched);
        var expected = new[]
        {
            (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Hsts, "stored entry for example.com"),
            (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Hsts, "learned Strict-Transport-Security from example.com"),
            (DiagnosticLogLevel.Info, DiagnosticLogComponents.Hsts, "http URL to example.com switched to https by its HSTS entry"),
        };
        diagnostics.Act("log lines", string.Join(" | ", log.Lines.Select(line => line.Message)));
        diagnostics.Assert("log line count", expected.Length, log.Lines.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Hsts, "stored entry for example.com"),
                (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Hsts, "learned Strict-Transport-Security from example.com"),
                (DiagnosticLogLevel.Info, DiagnosticLogComponents.Hsts, "http URL to example.com switched to https by its HSTS entry"),
            },
            log.Lines);
        bool leaksCredential = log.Lines.Any(line => line.Message.Contains("s3cret", StringComparison.Ordinal));
        diagnostics.Assert("a line leaks the password", false, leaksCredential);
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains("s3cret", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TrySwitchToHttps_UnknownHost_LogsTheMissAsVerbose()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        HstsTransferPolicy hsts = new(new FakeTimeProvider(Start), log);
        diagnostics.Arrange("url", UnknownUrl);

        bool switched = hsts.TrySwitchToHttps(UnknownUrl, CurlUrl.Parse(UnknownUrl), out _);

        diagnostics.Act("switched", switched);
        diagnostics.Assert("switched", false, switched);
        Assert.IsFalse(switched);
        diagnostics.Act("verbose lines", string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        diagnostics.Assert("verbose lines", "no HSTS entry for 127.0.0.1; http kept", string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(new[] { "no HSTS entry for 127.0.0.1; http kept" }, log.At(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public void TrySwitchToHttps_AtLogLevelError_RecordsNoInfoOrVerboseLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        HstsTransferPolicy hsts = new(new FakeTimeProvider(Start), log);
        hsts.StoreFromResponse(CurlUrl.Parse("https://example.com/"), "max-age=60", Start);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Error);

        hsts.TrySwitchToHttps("http://example.com/", CurlUrl.Parse("http://example.com/"), out _);
        hsts.TrySwitchToHttps(UnknownUrl, CurlUrl.Parse(UnknownUrl), out _);

        diagnostics.Act("line count", log.Lines.Count);
        diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }
}
