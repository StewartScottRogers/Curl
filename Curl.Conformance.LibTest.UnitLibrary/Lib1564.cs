using Curl.Api.EngineContracts.Multi;
using Libcurl.Global;
using Libcurl.Multi;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1564.c</c>: <c>curl_multi_wakeup</c> before <c>curl_multi_poll</c> on a multi handle with no easy handles.
/// Five polls of <see cref="PollTimeoutMilliseconds"/> (<see cref="Rounds"/>): with no wakeup the poll must last at
/// least <see cref="ThresholdMilliseconds"/>, after one wakeup or after ten at most that long, and a wakeup is used up
/// by the poll it ends. A poll on the wrong side writes <c>lib1564.c:N curl_multi_poll returned too early</c> (or
/// <c>too late</c>) on stderr and ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
/// <param name="timeProvider">Where the program's poll times and hang timeout are measured from.</param>
public sealed class Lib1564(TimeProvider timeProvider) : ILibTest
{
    /// <summary>How long each poll waits at most: <c>1000</c>.</summary>
    public const int PollTimeoutMilliseconds = 1000;

    /// <summary>The time a poll is held to: <c>500</c>.</summary>
    public const long ThresholdMilliseconds = 500;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// The program's polls in order: how many wakeups go before each, whether it must end early (a wakeup is
    /// pending) rather than wait, and the <c>lib1564.c</c> line of its failure's <c>curl_mfprintf</c>.
    /// </summary>
    public static IReadOnlyList<(int Wakeups, bool EndsEarly, int FailureLine)> Rounds { get; } =
        [(0, false, 51), (1, true, 68), (0, false, 83), (10, true, 101), (0, false, 116)];

    /// <inheritdoc/>
    public string ProgramName => "lib1564";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="cancellationToken">Stops the polls.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(clock);
        if (!steps.MultiInit(library, out CurlMulti? multi))
        {
            return;
        }

        await using (multi.ConfigureAwait(false))
        {
            foreach (var (wakeups, endsEarly, failureLine) in Rounds)
            {
                for (int i = 0; i < wakeups; i++)
                {
                    _ = steps.MultiWakeup(multi);
                }

                if (!await PollAsync(context, steps, multi, clock, endsEarly, failureLine, cancellationToken).ConfigureAwait(false)
                    || !steps.AbortOnTestTimeout(clock))
                {
                    return;
                }
            }
        }
    }

    private static async ValueTask<bool> PollAsync(LibTestContext context, LibTestSteps steps, CurlMulti multi, LibTestClock clock, bool endsEarly, int failureLine, CancellationToken cancellationToken)
    {
        long before = clock.Now();
        if (!(await steps.MultiPollAsync(multi, Memory<CurlWaitSocket>.Empty, PollTimeoutMilliseconds, cancellationToken).ConfigureAwait(false)).GoesOn)
        {
            return false;
        }

        long waited = clock.DifferenceMilliseconds(clock.Now(), before);
        bool tooLate = endsEarly && waited > ThresholdMilliseconds;
        bool tooEarly = !endsEarly && waited < ThresholdMilliseconds;
        if (!tooLate && !tooEarly)
        {
            return true;
        }

        context.StandardError.Write($"lib1564.c:{failureLine} curl_multi_poll returned too {(tooLate ? "late" : "early")}\n");
        steps.Result = LibTestErrorCodes.MajorBad;
        return false;
    }
}
