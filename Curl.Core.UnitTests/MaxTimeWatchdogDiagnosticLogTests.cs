using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Pins what <see cref="MaxTimeWatchdog" /> writes to Curl's own diagnostic log, component
/// <c>runner</c> (ADR-0222, BL-921): the <c>-m</c> limit armed as <c>verbose</c> and passed as
/// <c>error</c> naming <see cref="CurlExitCode.OperationTimedOut" />.
/// </summary>
[TestClass]
public sealed class MaxTimeWatchdogDiagnosticLogTests
{
    [TestMethod]
    public void TimerFired_MaxTimePassed_LogsAnErrorNamingOperationTimedOutAfterTheArming()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        HandFiredTimeProvider clock = new();
        using MaxTimeWatchdog watchdog = new(TimeSpan.FromSeconds(1), clock, log);

        clock.Advance(TimeSpan.FromMilliseconds(1004));
        clock.Fire();

        CollectionAssert.AreEqual(
            new[]
            {
                (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Runner, "--max-time 1000 ms armed"),
                (DiagnosticLogLevel.Error, DiagnosticLogComponents.Runner, "--max-time 1000 ms passed after 1004 ms; the attempt ends with exit 28 (OperationTimedOut)"),
            },
            log.Lines);
    }

    [TestMethod]
    public void StartFromCommandLine_WithALog_WritesTheArmingToIt()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        using MaxTimeWatchdog? watchdog = MaxTimeWatchdog.StartFromCommandLine(TimeSpan.FromSeconds(2), new HandFiredTimeProvider(), log);

        CollectionAssert.AreEqual(new[] { "--max-time 2000 ms armed" }, log.At(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public void TimerFired_AtLogLevelError_RecordsOnlyTheError()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        HandFiredTimeProvider clock = new();
        using MaxTimeWatchdog watchdog = new(TimeSpan.FromSeconds(1), clock, log);

        clock.Advance(TimeSpan.FromSeconds(1));
        clock.Fire();

        Assert.HasCount(1, log.Lines);
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines[0].Level);
    }
}
