using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Core.Hsts;

/// <summary>
/// Pins what <see cref="HstsCache" /> writes to Curl's own diagnostic log, component <c>hsts</c>
/// (ADR-0222, BL-1072): each entry stored from a header and each expired entry a lookup removes, at
/// <c>verbose</c>, by host name only.
/// </summary>
[TestClass]
public sealed class HstsCacheDiagnosticLogTests
{
    [TestMethod]
    public void ApplyHeader_ANewHost_LogsVerboseThatItsEntryIsStored()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        HstsCache cache = new(new FakeTimeProvider(Start), log);

        cache.ApplyHeader("max-age=60", "a.test.");

        Assert.AreEqual((DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Hsts, "stored entry for a.test"), log.Lines.Single());
    }

    [TestMethod]
    public void ApplyHeader_AHostHeld_LogsVerboseThatItsEntryIsStoredAgain()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        HstsCache cache = new(new FakeTimeProvider(Start), log);

        cache.ApplyHeader("max-age=60", "a.test");
        cache.ApplyHeader("max-age=120; includeSubDomains", "a.test");

        CollectionAssert.AreEqual(new[] { "stored entry for a.test", "stored entry for a.test" }, log.At(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public void Find_AnExpiredEntry_LogsVerboseThatItExpired()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        FakeTimeProvider clock = new(Start);
        HstsCache cache = new(clock, log);
        cache.ApplyHeader("max-age=60", "a.test");
        clock.Advance(TimeSpan.FromSeconds(60));

        Assert.IsNull(cache.Find("a.test"));

        Assert.AreEqual(
            (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Hsts, "entry for a.test expired"),
            log.Lines[^1]);
    }

    [TestMethod]
    public void EveryLine_BelowVerbose_IsNotWritten()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        FakeTimeProvider clock = new(Start);
        HstsCache cache = new(clock, log);

        cache.ApplyHeader("max-age=60", "a.test");
        clock.Advance(TimeSpan.FromSeconds(60));
        cache.Find("a.test");

        Assert.AreEqual(0, log.Lines.Count);
    }

    private static DateTimeOffset Start => new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
}
