using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core.AltSvc;

/// <summary>
/// Pins what <see cref="AltSvcCache" /> writes to Curl's own diagnostic log, component
/// <c>altsvc</c> (ADR-0222, BL-1072): an alternative used at <c>info</c>, each alternative stored
/// and each origin with none at <c>verbose</c>.
/// </summary>
[TestClass]
public sealed class AltSvcCacheDiagnosticLogTests
{
    private static readonly HashSet<AltSvcAlpn> AnyAlpn = [AltSvcAlpn.H1, AltSvcAlpn.H2, AltSvcAlpn.H3];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ApplyHeader_AnAlternative_LogsVerboseThatItIsStored()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);
        diagnostics.Arrange("header", "h3=\"alt.test:443\" from h2 a.test:8443");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        AltSvcCache cache = new(Clock(), log);

        cache.ApplyHeader("h3=\"alt.test:443\"", AltSvcAlpn.H2, "a.test", 8443);

        WriteLines(diagnostics, log);
        var expected = (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.AltSvc, "stored alternative h3 alt.test:443 for h2 a.test:8443");
        diagnostics.Assert("only line", expected, log.Lines.Single());
        Assert.AreEqual(expected, log.Lines.Single());
    }

    [TestMethod]
    public void FindForOrigin_AnAlternative_LogsInfoThatItIsUsed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Info);
        diagnostics.Arrange("header", "h3=\":443\" from h1 a.test:443");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        AltSvcCache cache = new(Clock(), log);
        cache.ApplyHeader("h3=\":443\"", AltSvcAlpn.H1, "a.test", 443);

        var found = cache.FindForOrigin([AltSvcAlpn.H2, AltSvcAlpn.H1], "a.test", 443, AnyAlpn);

        diagnostics.Act("found", found?.ToString() ?? "(null)");
        WriteLines(diagnostics, log);
        Assert.IsNotNull(found);
        var expected = (DiagnosticLogLevel.Info, DiagnosticLogComponents.AltSvc, "using alternative h3 a.test:443 for h1 a.test:443");
        diagnostics.Assert("only line", expected, log.Lines.Single());
        Assert.AreEqual(expected, log.Lines.Single());
    }

    [TestMethod]
    public void FindForOrigin_NoAlternative_LogsVerboseThatThereIsNone()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);
        diagnostics.Arrange("origin", "h1 a.test:443, cache empty");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        AltSvcCache cache = new(Clock(), log);

        var found = cache.FindForOrigin([AltSvcAlpn.H1], "a.test", 443, AnyAlpn);

        diagnostics.Act("found", found?.ToString() ?? "(null)");
        WriteLines(diagnostics, log);
        Assert.IsNull(found);
        var expected = (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.AltSvc, "no alternative for a.test:443");
        diagnostics.Assert("only line", expected, log.Lines.Single());
        Assert.AreEqual(expected, log.Lines.Single());
    }

    [TestMethod]
    public void EveryLine_BelowItsLevel_IsNotWritten()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Warning);
        diagnostics.Arrange("calls", "ApplyHeader, FindForOrigin a.test, FindForOrigin b.test");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);
        AltSvcCache cache = new(Clock(), log);

        cache.ApplyHeader("h3=\":443\"", AltSvcAlpn.H1, "a.test", 443);
        cache.FindForOrigin([AltSvcAlpn.H1], "a.test", 443, AnyAlpn);
        cache.FindForOrigin([AltSvcAlpn.H1], "b.test", 443, AnyAlpn);

        WriteLines(diagnostics, log);
        diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.AreEqual(0, log.Lines.Count);
    }

    private static void WriteLines(TestDiagnostics diagnostics, RecordingDiagnosticLog log)
    {
        diagnostics.Act("line count", log.Lines.Count);
        foreach (var line in log.Lines)
        {
            diagnostics.Act("line", line);
        }
    }

    private static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
}
