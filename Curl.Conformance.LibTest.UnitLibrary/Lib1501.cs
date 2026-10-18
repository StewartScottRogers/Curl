using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1501.c</c>: one verbose FTP transfer through a multi handle, driven by <c>multi_perform</c> and a 100 ms
/// <c>select()</c>, with a 30-second hang timeout. Around each <c>multi_perform</c> in the loop it writes <c>ping</c>
/// and <c>pong = N</c> (the milliseconds the call took) on stderr, and ends with <see cref="CurlCode.TooLarge"/> once
/// one call blocks for more than 500 ms. Cleanup type UA. Its <c>easy_setopt</c> checks are left out: Libcurl takes
/// these values without fail.
/// </summary>
/// <param name="timeProvider">Where the hang timeout and each call's time are measured from.</param>
public sealed class Lib1501(TimeProvider timeProvider) : ILibTest
{
    /// <summary><c>HANG_TIMEOUT</c>: the program's own hang timeout.</summary>
    public const long HangTimeoutMilliseconds = 30 * 1000;

    /// <summary><c>MAX_BLOCKED_TIME_MS</c>: the longest a <c>multi_perform</c> may block.</summary>
    public const long MaxBlockedMilliseconds = 500;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1501";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = steps.EasySetOptions(easy, [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url), (CurlOption.Verbose, 1L)]);
            if (!steps.MultiInit(library, out CurlMulti? multi))
            {
                return;
            }

            await using (multi.ConfigureAwait(false))
            {
                if (steps.MultiAddHandle(multi, easy))
                {
                    await LoopAsync(context, steps, multi, clock, cancellationToken).ConfigureAwait(false);
                }

                await multi.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask LoopAsync(LibTestContext context, LibTestSteps steps, CurlMulti multi, LibTestClock clock, CancellationToken cancellationToken)
    {
        var (goesOn, running) = await steps.MultiPerformAsync(multi, cancellationToken).ConfigureAwait(false);
        goesOn = goesOn && steps.AbortOnTestTimeout(clock, HangTimeoutMilliseconds);
        while (goesOn && running > 0)
        {
            if (!await steps.SelectTestAsync(multi, 100, cancellationToken).ConfigureAwait(false) || !steps.AbortOnTestTimeout(clock, HangTimeoutMilliseconds))
            {
                return;
            }

            context.StandardError.Write("ping\n");
            long before = clock.Now();
            (goesOn, running) = await steps.MultiPerformAsync(multi, cancellationToken).ConfigureAwait(false);
            goesOn = goesOn && steps.AbortOnTestTimeout(clock, HangTimeoutMilliseconds);
            goesOn = goesOn && WithinBlockingLimit(context, steps, clock.DifferenceMilliseconds(clock.Now(), before));
        }
    }

    private static bool WithinBlockingLimit(LibTestContext context, LibTestSteps steps, long elapsed)
    {
        context.StandardError.Write($"pong = {elapsed}\n");
        if (elapsed <= MaxBlockedMilliseconds)
        {
            return true;
        }

        steps.Result = CurlCode.TooLarge;
        return false;
    }
}
