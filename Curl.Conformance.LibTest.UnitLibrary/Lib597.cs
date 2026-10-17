using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib597.c</c>, which case 660 runs too: a verbose <c>CURLOPT_CONNECT_ONLY</c> transfer through a multi handle,
/// driven as <c>lib591.c</c>'s (<see cref="Lib591.TransferAsync"/>) but with 59 seconds when there is no timeout,
/// answering the transfer's result once the handle is removed: connect-only must not block once the protocol
/// connected.
/// </summary>
/// <param name="timeProvider">The program's clock, for <c>abort_on_test_timeout</c>.</param>
public sealed class Lib597(TimeProvider timeProvider) : ILibTest
{
    // (TEST_HANG_TIMEOUT / 1000) - 1 seconds.
    private const int NoTimeoutMilliseconds = 59_000;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib597";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, cancellationToken));
    }

    /// <summary>The body after <c>global_init</c>: <c>easy_init</c>, <c>multi_init</c>, the options, the loop and the remove.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
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
            if (!steps.MultiInit(library, out CurlMulti? multi))
            {
                return;
            }

            await using (multi.ConfigureAwait(false))
            {
                _ = steps.EasySetOptions(easy, [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Url, context.Url),
                    (CurlOption.ConnectOnly, 1L),
                ]);
                if (await Lib591.TransferAsync(steps, easy, multi, clock, NoTimeoutMilliseconds, cancellationToken).ConfigureAwait(false))
                {
                    _ = steps.MultiRemoveHandle(multi, easy);
                }
            }
        }
    }
}
