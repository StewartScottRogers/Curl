using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core.Hsts;

/// <summary>
/// Pins what <see cref="HstsCache" /> writes to Curl's own diagnostic log, component <c>hsts</c>
/// (ADR-0222, BL-1072): each entry stored from a header and each expired entry a lookup removes, at
/// <c>verbose</c>, by host name only.
/// </summary>
[TestClass]
public sealed class HstsCacheDiagnosticLogTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ApplyHeader_ANewHost_LogsVerboseThatItsEntryIsStored()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        HstsCache cache = new(new FakeTimeProvider(Start), log);
        diagnostics.Arrange("clock", Start);
        diagnostics.Arrange("header", "max-age=60 for a.test.");

        cache.ApplyHeader("max-age=60", "a.test.");

        var expected = (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Hsts, "stored entry for a.test");
        diagnostics.Act("log line", log.Lines.Single());
        diagnostics.Assert("log line", expected, log.Lines.Single());
        Assert.AreEqual(expected, log.Lines.Single());
    }

    [TestMethod]
    public void ApplyHeader_AHostHeld_LogsVerboseThatItsEntryIsStoredAgain()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        HstsCache cache = new(new FakeTimeProvider(Start), log);
        diagnostics.Arrange("headers", "max-age=60, then max-age=120; includeSubDomains, for a.test");

        cache.ApplyHeader("max-age=60", "a.test");
        cache.ApplyHeader("max-age=120; includeSubDomains", "a.test");

        var expected = new[] { "stored entry for a.test", "stored entry for a.test" };
        diagnostics.Act("verbose lines", string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        diagnostics.Assert("verbose lines", string.Join(" | ", expected), string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(expected, log.At(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public void Find_AnExpiredEntry_LogsVerboseThatItExpired()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        FakeTimeProvider clock = new(Start);
        HstsCache cache = new(clock, log);
        cache.ApplyHeader("max-age=60", "a.test");
        diagnostics.Arrange("header", "max-age=60 for a.test");
        diagnostics.Arrange("clock advance", "60 seconds");
        clock.Advance(TimeSpan.FromSeconds(60));

        HstsEntry? found = cache.Find("a.test");

        var expected = (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Hsts, "entry for a.test expired");
        diagnostics.Act("found", found?.Host ?? "null");
        diagnostics.Assert("found", "null", found?.Host ?? "null");
        Assert.IsNull(found);

        diagnostics.Act("last log line", log.Lines[^1]);
        diagnostics.Assert("last log line", expected, log.Lines[^1]);
        Assert.AreEqual(
            (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Hsts, "entry for a.test expired"),
            log.Lines[^1]);
    }

    [TestMethod]
    public void EveryLine_BelowVerbose_IsNotWritten()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        FakeTimeProvider clock = new(Start);
        HstsCache cache = new(clock, log);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Info);

        cache.ApplyHeader("max-age=60", "a.test");
        diagnostics.Arrange("clock advance", "60 seconds");
        clock.Advance(TimeSpan.FromSeconds(60));
        cache.Find("a.test");

        diagnostics.Act("line count", log.Lines.Count);
        diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.AreEqual(0, log.Lines.Count);
    }

    private static DateTimeOffset Start => new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
}
