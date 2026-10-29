using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-294: under <c>-Z --fail-early</c> curl 8.22.0 stops starting transfers at the first failure
/// but aborts the running ones only about a second later, so one whose response arrives first completes.
/// </summary>
[TestClass]
public sealed class Cgr294Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void RecordEnd_FailEarly_StopsStartsAtOnceButDoesNotAbortRunningTransfersYet()
    {
        Diagnostics.Arrange("run", "one run-ending failure under --fail-early");
        ParallelRun run = new(3, 0, true);

        run.RecordEnd(TransferResult.Failure(CurlExitCode.HttpReturnedError, "500"), endsTheRun: true, failEarly: true);
        Diagnostics.Act("has failed early / is aborted", $"{run.HasFailedEarly}/{run.IsAborted}");

        Diagnostics.Assert("has failed early / is aborted", "True/False", $"{run.HasFailedEarly}/{run.IsAborted}");
        Assert.IsTrue(run.HasFailedEarly);
        Assert.IsFalse(run.IsAborted);
    }

    [TestMethod]
    public void AbortDelay_IsOneSecond()
    {
        Diagnostics.Arrange("delay", "curl's once-a-second abort check");

        Diagnostics.Assert("delay", "00:00:01", ParallelRun.AbortDelay.ToString());
        Assert.AreEqual(TimeSpan.FromSeconds(1), ParallelRun.AbortDelay);
    }
}
