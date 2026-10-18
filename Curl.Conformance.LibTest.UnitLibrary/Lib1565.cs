using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1565.c</c> as built with threads: a second thread makes <see cref="ConnectionCount"/> easy handles for the
/// case's URL, one every <c>TIME_BETWEEN_START_SECS</c>, hands each over under a lock and wakes the main thread's
/// <c>curl_multi_poll</c> of <c>TEST_HANG_TIMEOUT</c> with <c>curl_multi_wakeup</c>; the main thread adds every handle
/// handed over and ends once all have finished. The program answers the first failure either thread saw. Upstream's
/// <c>pthread_create</c> check and its two "Not all connections" lines are left out: a task always starts, and the loop
/// ends only once every handle finished, which it cannot before every handle started. Libcurl's only message is
/// <c>CURLMSG_DONE</c>, so the unexpected-message line is left out too, as are the second thread's <c>easy_init</c> and
/// <c>easy_setopt</c> checks (they cannot fail while the main thread holds the library) and the hang-timeout checks after
/// each message and after the handles are added (only list work runs between them and the check before).
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from.</param>
/// <param name="timeBetweenStartsMilliseconds">How long the second thread waits before each handle: <c>TIME_BETWEEN_START_SECS</c>, 2000 ms upstream.</param>
public sealed class Lib1565(TimeProvider timeProvider, int timeBetweenStartsMilliseconds = Lib1565.TimeBetweenStartsMilliseconds) : ILibTest
{
    /// <summary>How many handles the second thread makes: <c>CONN_NUM</c>.</summary>
    public const int ConnectionCount = 3;

    /// <summary>Upstream's wait before each handle: <c>TIME_BETWEEN_START_SECS</c> in milliseconds.</summary>
    public const int TimeBetweenStartsMilliseconds = 2000;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1565";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, timeBetweenStartsMilliseconds, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps; it ends holding the program's code, <c>t1565_test_failure</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="timeBetweenStartsMilliseconds">How long the second thread waits before each handle.</param>
    /// <param name="cancellationToken">Stops the transfers and the waits.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, int timeBetweenStartsMilliseconds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(clock);
        if (!steps.MultiInit(library, out CurlMulti? multi))
        {
            return;
        }

        var shared = new SharedState();
        var started = new List<CurlEasy>();
        Task thread = Task.Run(() => MakeHandlesAsync(context, library, multi, shared, clock, timeBetweenStartsMilliseconds, cancellationToken), cancellationToken);
        await TransferAsync(steps, multi, shared, started, clock, cancellationToken).ConfigureAwait(false);
        shared.RecordFailure(steps.Result);
        await thread.ConfigureAwait(false);
        await multi.DisposeAsync().ConfigureAwait(false);
        foreach (CurlEasy easy in shared.Pending.Concat(started))
        {
            await easy.DisposeAsync().ConfigureAwait(false);
        }

        steps.Result = shared.Failure;
    }

    private static async Task MakeHandlesAsync(LibTestContext context, CurlLibrary library, CurlMulti multi, SharedState shared, LibTestClock clock, int waitMilliseconds, CancellationToken cancellationToken)
    {
        var steps = new LibTestSteps(context.StandardError);
        for (int i = 0; i < ConnectionCount; i++)
        {
            await clock.WaitAsync(waitMilliseconds, cancellationToken).ConfigureAwait(false);
            CurlEasy easy = library.EasyInit();
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Verbose, 0L);
            if (!shared.TryHandOver(easy))
            {
                await easy.DisposeAsync().ConfigureAwait(false);
                break;
            }

            _ = steps.MultiWakeup(multi);
        }

        shared.RecordFailure(steps.Result);
    }

    private static async ValueTask TransferAsync(LibTestSteps steps, CurlMulti multi, SharedState shared, List<CurlEasy> started, LibTestClock clock, CancellationToken cancellationToken)
    {
        int finished = 0;
        while (true)
        {
            if (!(await steps.MultiPerformAsync(multi, cancellationToken).ConfigureAwait(false)).GoesOn
                || !steps.AbortOnTestTimeout(clock)
                || !ReadMessages(steps, multi, ref finished))
            {
                return;
            }

            if (finished == ConnectionCount)
            {
                return;
            }

            if (!(await steps.MultiPollAsync(multi, Memory<CurlWaitSocket>.Empty, (int)LibTestClock.TestHangTimeoutMilliseconds, cancellationToken).ConfigureAwait(false)).GoesOn
                || !steps.AbortOnTestTimeout(clock)
                || !shared.AddPending(steps, multi, started))
            {
                return;
            }
        }
    }

    private static bool ReadMessages(LibTestSteps steps, CurlMulti multi, ref int finished)
    {
        while (multi.InfoRead(out _) is { } message)
        {
            steps.Result = message.Result;
            if (steps.Result != CurlCode.Ok || !steps.MultiRemoveHandle(multi, message.Easy))
            {
                return false;
            }

            finished++;
        }

        return true;
    }

    /// <summary>What the two threads share under <c>lock</c>: the handles handed over and the first failure.</summary>
    private sealed class SharedState
    {
        private readonly Lock gate = new();

        public List<CurlEasy> Pending { get; } = [];

        public CurlCode Failure { get; private set; }

        public void RecordFailure(CurlCode code)
        {
            lock (gate)
            {
                Failure = Failure == CurlCode.Ok ? code : Failure;
            }
        }

        public bool TryHandOver(CurlEasy easy)
        {
            lock (gate)
            {
                if (Failure != CurlCode.Ok)
                {
                    return false;
                }

                Pending.Add(easy);
                return true;
            }
        }

        public bool AddPending(LibTestSteps steps, CurlMulti multi, List<CurlEasy> started)
        {
            lock (gate)
            {
                while (Pending.Count > 0)
                {
                    CurlEasy easy = Pending[^1];
                    if (!steps.MultiAddHandle(multi, easy))
                    {
                        return false;
                    }

                    started.Add(easy);
                    Pending.RemoveAt(Pending.Count - 1);
                }

                return true;
            }
        }
    }
}
