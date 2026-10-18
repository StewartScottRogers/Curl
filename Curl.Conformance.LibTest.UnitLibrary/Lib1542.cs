using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1542.c</c>: four verbose, traced GETs on one handle: two at once, one after a two-second wait, which still
/// reuses the connection, and one after <c>CURLOPT_MAXLIFETIME_CONN</c> is set to one second, which must not. The
/// first failing transfer's code is the program's.
/// </summary>
/// <param name="timeProvider">The clock behind the two-second wait.</param>
public sealed class Lib1542(TimeProvider timeProvider) : ILibTest
{
    /// <summary>How long the program waits before the third transfer, so the connection is two seconds old.</summary>
    public const int WaitMilliseconds = 2000;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1542";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, new LibTestClock(timeProvider), timeProvider, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, for <c>curlx_wait_ms</c>.</param>
    /// <param name="timeProvider">Where the trace's times come from (unused: <c>tracetime</c> is off).</param>
    /// <param name="cancellationToken">Stops the transfers and the wait.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(clock);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: false, noHex: true);
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = steps.EasySetOptions(easy, [
                (CurlOption.Url, context.Url),
                (CurlOption.Debugdata, trace),
                (CurlOption.Debugfunction, (DebugCallback)trace.Trace),
                (CurlOption.Verbose, 1L),
            ]);
            if (!await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false)
                || !await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await clock.WaitAsync(WaitMilliseconds, cancellationToken).ConfigureAwait(false);
            _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false)
                && steps.EasySetOption(easy, CurlOption.MaxlifetimeConn, 1L)
                && await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
        }
    }
}
