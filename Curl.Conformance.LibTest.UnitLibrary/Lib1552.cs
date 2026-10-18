using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1552.c</c>: one verbose transfer with headers and the credentials <c>u:s</c> through a multi handle, driven by
/// <c>multi_perform</c> and at most three <c>curl_multi_wait</c>s of <c>TEST_HANG_TIMEOUT</c>
/// (<see cref="WaitLoopAsync"/>), answering the transfer's own code from <c>curl_multi_info_read</c>
/// (<c>CURLE_OK</c> when there is no message yet) unless a step failed. Cleanup type UA: <c>curl_multi_cleanup</c>
/// with the handle still added, then <c>curl_easy_cleanup</c>; case 1552 checks an IMAP transfer error survives it.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from.</param>
public sealed class Lib1552(TimeProvider timeProvider) : ILibTest
{
    /// <summary>How many <c>curl_multi_wait</c> turns the loop takes at most: <c>counter</c>.</summary>
    public const int WaitTurns = 3;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1552";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>; it ends holding the program's code.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.MultiInit(library, out CurlMulti? multi))
        {
            return;
        }

        await using (multi.ConfigureAwait(false))
        {
            if (!steps.EasyInit(library, out CurlEasy? easy))
            {
                return;
            }

            await using (easy.ConfigureAwait(false))
            {
                if (steps.SetOptionsAndAddHandle(
                        easy,
                        [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url), (CurlOption.Header, 1L), (CurlOption.Verbose, 1L), (CurlOption.Userpwd, "u:s")],
                        multi)
                    && await WaitLoopAsync(context, steps, multi, clock, WaitTurns, cancellationToken).ConfigureAwait(false))
                {
                    steps.Result = multi.InfoRead(out _)?.Result ?? CurlCode.Ok;
                }

                await multi.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The loop <c>lib1552.c</c> and <c>lib1553.c</c> share: <c>multi_perform</c> and <c>abort_on_test_timeout</c>,
    /// then, while transfers run and <paramref name="turns"/> are left, a <c>curl_multi_wait</c> of
    /// <c>TEST_HANG_TIMEOUT</c> followed by the same perform and timeout checks. A failing wait writes
    /// <c>curl_multi_wait() returned N</c> on stdout and sets <see cref="LibTestErrorCodes.MajorBad"/>.
    /// </summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="multi">The multi handle, with its transfer added.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="turns">How many waits the loop takes at most: <c>counter</c>.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>Whether the program goes on (no step failed).</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<bool> WaitLoopAsync(LibTestContext context, LibTestSteps steps, CurlMulti multi, LibTestClock clock, int turns, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(multi);
        var (goesOn, running) = await steps.PerformAndCheckTimeoutAsync(multi, clock, cancellationToken).ConfigureAwait(false);
        while (goesOn && running > 0 && turns-- > 0)
        {
            var (code, _) = await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, (int)LibTestClock.TestHangTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);
            if (code != CurlMultiCode.Ok)
            {
                using StreamWriter output = LibTestRun.StandardOutputWriter(context);
                output.Write($"curl_multi_wait() returned {(int)code}\n");
                steps.Result = LibTestErrorCodes.MajorBad;
                return false;
            }

            (goesOn, running) = steps.AbortOnTestTimeout(clock)
                ? await steps.PerformAndCheckTimeoutAsync(multi, clock, cancellationToken).ConfigureAwait(false)
                : (false, 0);
        }

        return goesOn;
    }
}
