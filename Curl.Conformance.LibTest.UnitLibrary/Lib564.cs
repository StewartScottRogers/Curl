using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib564.c</c>: a verbose transfer of the case's URL through the SOCKS4 proxy <c>libtest_arg2</c>, traced by
/// <c>libtest_debug_cb</c> with no hex and with time, through a multi handle's <c>select()</c> loop of one
/// second; <c>Start at URL 0</c> goes to stderr once the handle is added.
/// </summary>
/// <param name="timeProvider">The program's clock, for <c>abort_on_test_timeout</c> and the trace's time.</param>
public sealed class Lib564(TimeProvider timeProvider) : ILibTest
{
    /// <summary><c>CURLPROXY_SOCKS4</c>.</summary>
    public const long ProxySocks4 = 4;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib564";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, timeProvider, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>; cleanup type UB.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="timeProvider">The trace's clock.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(
        LibTestContext context,
        LibTestSteps steps,
        CurlLibrary library,
        LibTestClock clock,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
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
                var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);
                bool added = steps.SetOptionsAndAddHandle(
                    easy,
                    [
                        (CurlOption.Writedata, context.StandardOutput),
                        (CurlOption.Url, context.Url),
                        (CurlOption.Debugdata, trace),
                        (CurlOption.Debugfunction, (DebugCallback)trace.Trace),
                        (CurlOption.Verbose, 1L),
                        (CurlOption.Proxy, context.Argument2),
                        (CurlOption.Proxytype, ProxySocks4),
                    ],
                    multi);
                if (added)
                {
                    await context.StandardError.WriteAsync("Start at URL 0\n").ConfigureAwait(false);
                    await steps.PerformUntilDoneAsync(multi, clock, 1000, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
