using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins what <see cref="LowSpeedWatchdog" /> writes to Curl's own diagnostic log, component
/// <c>runner</c> (ADR-0222, BL-921): the <c>-Y</c>/<c>-y</c> limit armed as <c>verbose</c> and hit
/// as <c>error</c> naming <see cref="CurlExitCode.OperationTimedOut" />.
/// </summary>
[TestClass]
public sealed class LowSpeedWatchdogDiagnosticLogTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void StartFromCommandLine_StalledTransfer_LogsAnErrorNamingOperationTimedOutAfterTheArming()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        TickingTimeProvider clock = new();
        using LowSpeedWatchdog? watchdog = LowSpeedWatchdog.StartFromCommandLine(100, 2, clock, log);

        diagnostics.Arrange("tick seconds", 3);
        clock.Tick(3);

        var expected = new[]
        {
            (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Runner, "--speed-limit 100 bytes/s over --speed-time 2 s armed"),
            (DiagnosticLogLevel.Error, DiagnosticLogComponents.Runner, "--speed-limit 100 bytes/s not reached for 2 s; the attempt ends with exit 28 (OperationTimedOut)"),
        };
        diagnostics.Act("log lines", string.Join(" | ", log.Lines));
        diagnostics.Assert("log lines", string.Join(" | ", expected), string.Join(" | ", log.Lines));
        CollectionAssert.AreEqual(
            expected,
            log.Lines);
    }

    [TestMethod]
    public void StalledTransfer_AtLogLevelError_RecordsOnlyTheError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Error);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        TickingTimeProvider clock = new();
        using LowSpeedWatchdog watchdog = new(100, TimeSpan.FromSeconds(2), clock, log);

        diagnostics.Arrange("tick seconds", 3);
        clock.Tick(3);

        diagnostics.Act("line count", log.Lines.Count);
        diagnostics.Assert("line count", 1, log.Lines.Count);
        Assert.HasCount(1, log.Lines);
        diagnostics.Act("first line level", log.Lines[0].Level);
        diagnostics.Assert("first line level", DiagnosticLogLevel.Error, log.Lines[0].Level);
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines[0].Level);
    }
}
