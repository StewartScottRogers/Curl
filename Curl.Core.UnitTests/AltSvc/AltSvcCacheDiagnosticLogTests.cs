using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void ApplyHeader_AnAlternative_LogsVerboseThatItIsStored()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        AltSvcCache cache = new(Clock(), log);

        cache.ApplyHeader("h3=\"alt.test:443\"", AltSvcAlpn.H2, "a.test", 8443);

        Assert.AreEqual(
            (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.AltSvc, "stored alternative h3 alt.test:443 for h2 a.test:8443"),
            log.Lines.Single());
    }

    [TestMethod]
    public void FindForOrigin_AnAlternative_LogsInfoThatItIsUsed()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        AltSvcCache cache = new(Clock(), log);
        cache.ApplyHeader("h3=\":443\"", AltSvcAlpn.H1, "a.test", 443);

        Assert.IsNotNull(cache.FindForOrigin([AltSvcAlpn.H2, AltSvcAlpn.H1], "a.test", 443, AnyAlpn));

        Assert.AreEqual(
            (DiagnosticLogLevel.Info, DiagnosticLogComponents.AltSvc, "using alternative h3 a.test:443 for h1 a.test:443"),
            log.Lines.Single());
    }

    [TestMethod]
    public void FindForOrigin_NoAlternative_LogsVerboseThatThereIsNone()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        AltSvcCache cache = new(Clock(), log);

        Assert.IsNull(cache.FindForOrigin([AltSvcAlpn.H1], "a.test", 443, AnyAlpn));

        Assert.AreEqual(
            (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.AltSvc, "no alternative for a.test:443"),
            log.Lines.Single());
    }

    [TestMethod]
    public void EveryLine_BelowItsLevel_IsNotWritten()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);
        AltSvcCache cache = new(Clock(), log);

        cache.ApplyHeader("h3=\":443\"", AltSvcAlpn.H1, "a.test", 443);
        cache.FindForOrigin([AltSvcAlpn.H1], "a.test", 443, AnyAlpn);
        cache.FindForOrigin([AltSvcAlpn.H1], "b.test", 443, AnyAlpn);

        Assert.AreEqual(0, log.Lines.Count);
    }

    private static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
}
