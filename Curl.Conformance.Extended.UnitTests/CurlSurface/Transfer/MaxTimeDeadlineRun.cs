using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer;

/// <summary>
/// Drives a <c>-m</c> check's injected clock past curl's deadline: one second at a time, until curl's run ends, so
/// every transfer-surface max-time check ends on its clock and never at the 30-second wait.
/// </summary>
internal static class MaxTimeDeadlineRun
{
    /// <summary>The most one-second steps the clock takes before the check stops advancing it and waits for the run.</summary>
    internal const int MostSteps = 3000;

    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan PauseBetweenSteps = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Advances the clock through <paramref name="advance" /> one second at a time until <paramref name="run" />
    /// completes, then returns its result.
    /// </summary>
    /// <remarks>
    /// The loop is bounded by steps, never by the <c>-m</c> seconds (BL-4663): under a full fast run curl can reach a
    /// timer on the clock (its <c>-m</c> watchdog re-armed, or a delay its shutdown awaits after the watchdog fired)
    /// only after the clock has already moved past the deadline. A loop that stopped after <c>-m</c> + 1 seconds then
    /// left that timer unfired and the run waited 30 seconds. Each step also gives curl real time to run between
    /// advances, since <see cref="Task.Yield" /> alone is not enough under load.
    /// </remarks>
    internal static async Task<CurlRunResult> AdvanceUntilDoneAsync(Task<CurlRunResult> run, Action<TimeSpan> advance, CancellationToken cancellationToken = default)
    {
        for (int step = 0; step < MostSteps && !run.IsCompleted; step++)
        {
            advance(Step);
            await Task.WhenAny(run, Task.Delay(PauseBetweenSteps, cancellationToken));
        }

        return await run.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
    }
}
