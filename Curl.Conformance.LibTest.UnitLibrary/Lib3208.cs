using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib3208.c</c>: one verbose handle with headers and neither the peer nor the host verified, used three times on the
/// case's URL: an easy perform, then a multi perform (the loop <see cref="Lib1500.LoopAsync"/>), then, removed from the
/// multi handle, an easy perform again. The program answers the multi transfer's own code from
/// <c>curl_multi_info_read</c> (<see cref="LibTestErrorCodes.Failure"/> when there is no message) unless a step failed;
/// the two easy performs' codes are ignored. Cleanup type UA: <c>curl_multi_cleanup</c>, then <c>curl_easy_cleanup</c>.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from.</param>
public sealed class Lib3208(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib3208";

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
    /// <param name="cancellationToken">Stops the transfers.</param>
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
                CurlCode programResult = await TransfersAsync(context, steps, multi, easy, clock, cancellationToken).ConfigureAwait(false);
                await multi.DisposeAsync().ConfigureAwait(false);
                steps.Result = steps.Result == CurlCode.Ok ? programResult : steps.Result;
            }
        }
    }

    private static async ValueTask<CurlCode> TransfersAsync(LibTestContext context, LibTestSteps steps, CurlMulti multi, CurlEasy easy, LibTestClock clock, CancellationToken cancellationToken)
    {
        (CurlOption, object?)[] options =
        [
            (CurlOption.Writedata, context.StandardOutput),
            (CurlOption.Url, context.Url),
            (CurlOption.Header, 1L),
            (CurlOption.Verbose, 1L),
            (CurlOption.SslVerifypeer, 0L),
            (CurlOption.SslVerifyhost, 0L),
        ];
        // Libcurl takes these easy_setopt values without fail, so the macros' jumps to test_cleanup are left out.
        LibTestRun.SetOptionsUnchecked(easy, options);
        _ = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        if (!steps.MultiAddHandle(multi, easy) || !await Lib1500.LoopAsync(context, steps, multi, clock, cancellationToken).ConfigureAwait(false))
        {
            return LibTestErrorCodes.Failure;
        }

        CurlCode programResult = multi.InfoRead(out _)?.Result ?? LibTestErrorCodes.Failure;
        _ = multi.RemoveHandle(easy);
        _ = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        return programResult;
    }
}
