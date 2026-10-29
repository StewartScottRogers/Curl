using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins BL-4062: under <c>-Z --fail-early</c> the abort delay starts at the first failure that ends the
/// run, on the runner's <see cref="TimeProvider" />, and a later failure does not move it.
/// </summary>
[TestClass]
public sealed class ParallelRunAbortDelayTests
{
    private static readonly TransferResult Refused = TransferResult.Failure(CurlExitCode.CouldntConnect, "refused");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void RecordEnd_SecondFailureWithinTheDelay_DoesNotMoveTheAbort()
    {
        Diagnostics.Arrange("run", "two run-ending failures 600 ms apart under --fail-early, on a fake clock");
        Cgr109Tests.TimerFiringClock clock = new();
        ParallelRun run = new(3, 0, true, clock);

        run.RecordEnd(Refused, endsTheRun: true, failEarly: true);
        clock.Advance(600);
        run.RecordEnd(Refused, endsTheRun: true, failEarly: true);
        bool abortedBeforeTheDelay = run.IsAborted;
        clock.Advance(400);
        Diagnostics.Act("aborted at 600 ms / at 1000 ms", $"{abortedBeforeTheDelay}/{run.IsAborted}");

        Diagnostics.Assert("aborted at 600 ms / at 1000 ms", "False/True", $"{abortedBeforeTheDelay}/{run.IsAborted}");
        Assert.IsFalse(abortedBeforeTheDelay);
        Assert.IsTrue(run.IsAborted);
    }
}
