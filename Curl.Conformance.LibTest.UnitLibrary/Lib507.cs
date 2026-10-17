using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib507.c</c>: one URL with headers through a multi handle, driven by <c>multi_perform</c> and a one-second
/// <c>select()</c> until it ends, answering the transfer's own code from <c>curl_multi_info_read</c>
/// (<see cref="LibTestErrorCodes.MajorBad"/> when there is no message) unless a step failed. Cleanup type UA:
/// <c>curl_multi_cleanup</c> with the handle still added, then <c>curl_easy_cleanup</c>.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from.</param>
public sealed class Lib507(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib507";

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
                if (steps.SetOptionsAndAddHandle(easy, [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url), (CurlOption.Header, 1L)], multi)
                    && await steps.PerformUntilDoneAsync(multi, clock, 1000, cancellationToken).ConfigureAwait(false))
                {
                    CurlMessage? message = multi.InfoRead(out _);
                    steps.Result = message?.Result ?? LibTestErrorCodes.MajorBad;
                }

                await multi.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
