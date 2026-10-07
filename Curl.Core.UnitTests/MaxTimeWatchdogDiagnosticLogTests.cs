using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins what <see cref="MaxTimeWatchdog" /> writes to Curl's own diagnostic log, component
/// <c>runner</c> (ADR-0222, BL-921): the <c>-m</c> limit armed as <c>verbose</c> and passed as
/// <c>error</c> naming <see cref="CurlExitCode.OperationTimedOut" />.
/// </summary>
[TestClass]
public sealed class MaxTimeWatchdogDiagnosticLogTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TimerFired_MaxTimePassed_LogsAnErrorNamingOperationTimedOutAfterTheArming()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        HandFiredTimeProvider clock = new();
        using MaxTimeWatchdog watchdog = new(TimeSpan.FromSeconds(1), clock, log);

        diagnostics.Arrange("advance", TimeSpan.FromMilliseconds(1004));
        clock.Advance(TimeSpan.FromMilliseconds(1004));
        clock.Fire();

        var expected = new[]
        {
            (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Runner, "--max-time 1000 ms armed"),
            (DiagnosticLogLevel.Error, DiagnosticLogComponents.Runner, "--max-time 1000 ms passed after 1004 ms; the attempt ends with exit 28 (OperationTimedOut)"),
        };
        diagnostics.Act("log lines", string.Join(" | ", log.Lines));
        diagnostics.Assert("log lines", string.Join(" | ", expected), string.Join(" | ", log.Lines));
        CollectionAssert.AreEqual(
            expected,
            log.Lines);
    }

    [TestMethod]
    public void StartFromCommandLine_WithALog_WritesTheArmingToIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);
        diagnostics.Arrange("max time", TimeSpan.FromSeconds(2));
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        using MaxTimeWatchdog? watchdog = MaxTimeWatchdog.StartFromCommandLine(TimeSpan.FromSeconds(2), new HandFiredTimeProvider(), log);

        var expected = new[] { "--max-time 2000 ms armed" };
        diagnostics.Act("verbose lines", string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        diagnostics.Assert("verbose lines", string.Join(" | ", expected), string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(expected, log.At(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public void TimerFired_AtLogLevelError_RecordsOnlyTheError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Error);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        HandFiredTimeProvider clock = new();
        using MaxTimeWatchdog watchdog = new(TimeSpan.FromSeconds(1), clock, log);

        diagnostics.Arrange("advance", TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(1));
        clock.Fire();

        diagnostics.Act("line count", log.Lines.Count);
        diagnostics.Assert("line count", 1, log.Lines.Count);
        Assert.HasCount(1, log.Lines);
        diagnostics.Act("first line level", log.Lines[0].Level);
        diagnostics.Assert("first line level", DiagnosticLogLevel.Error, log.Lines[0].Level);
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines[0].Level);
    }
}
