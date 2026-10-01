using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Pins what <see cref="LowSpeedWatchdog" /> writes to Curl's own diagnostic log, component
/// <c>runner</c> (ADR-0222, BL-921): the <c>-Y</c>/<c>-y</c> limit armed as <c>verbose</c> and hit
/// as <c>error</c> naming <see cref="CurlExitCode.OperationTimedOut" />.
/// </summary>
[TestClass]
public sealed class LowSpeedWatchdogDiagnosticLogTests
{
    [TestMethod]
    public void StartFromCommandLine_StalledTransfer_LogsAnErrorNamingOperationTimedOutAfterTheArming()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        TickingTimeProvider clock = new();
        using LowSpeedWatchdog? watchdog = LowSpeedWatchdog.StartFromCommandLine(100, 2, clock, log);

        clock.Tick(3);

        CollectionAssert.AreEqual(
            new[]
            {
                (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Runner, "--speed-limit 100 bytes/s over --speed-time 2 s armed"),
                (DiagnosticLogLevel.Error, DiagnosticLogComponents.Runner, "--speed-limit 100 bytes/s not reached for 2 s; the attempt ends with exit 28 (OperationTimedOut)"),
            },
            log.Lines);
    }

    [TestMethod]
    public void StalledTransfer_AtLogLevelError_RecordsOnlyTheError()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        TickingTimeProvider clock = new();
        using LowSpeedWatchdog watchdog = new(100, TimeSpan.FromSeconds(2), clock, log);

        clock.Tick(3);

        Assert.HasCount(1, log.Lines);
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines[0].Level);
    }
}
