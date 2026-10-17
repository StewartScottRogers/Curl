using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib533.c</c>: one handle with <c>CURLOPT_FAILONERROR</c> through a multi handle for the URL, then removed,
/// reset with <c>curl_easy_reset</c> and re-added for the second argument's URL (<c>Start at URL 0</c> and
/// <c>Advancing to URL 1</c> on stderr), driven by <c>multi_perform</c> and a one-second <c>select()</c> until
/// both end. Cleanup type UB: <c>curl_easy_cleanup</c> then <c>curl_multi_cleanup</c>.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from.</param>
public sealed class Lib533(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib533";

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
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        CurlMulti? multi = null;
        if (steps.MultiInit(library, out multi) && steps.SetOptionsAndAddHandle(easy, OptionsOf(context, context.Url), multi))
        {
            context.StandardError.Write("Start at URL 0\n");
            await TransferBothAsync(context, steps, clock, multi, easy, cancellationToken).ConfigureAwait(false);
        }

        await easy.DisposeAsync().ConfigureAwait(false);
        await (multi?.DisposeAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
    }

    private static async ValueTask TransferBothAsync(LibTestContext context, LibTestSteps steps, LibTestClock clock, CurlMulti multi, CurlEasy easy, CancellationToken cancellationToken)
    {
        int current = 0;
        while (true)
        {
            var (goesOn, running) = await steps.PerformAndCheckTimeoutAsync(multi, clock, cancellationToken).ConfigureAwait(false);
            if (!goesOn || (running == 0 && (current++ != 0 || !ReAdd(context, steps, multi, easy))))
            {
                return;
            }

            if (!await steps.SelectTestAsync(multi, 1000, cancellationToken).ConfigureAwait(false) || !steps.AbortOnTestTimeout(clock))
            {
                return;
            }
        }
    }

    private static bool ReAdd(LibTestContext context, LibTestSteps steps, CurlMulti multi, CurlEasy easy)
    {
        context.StandardError.Write("Advancing to URL 1\n");
        multi.RemoveHandle(easy);
        easy.Reset();
        return steps.SetOptionsAndAddHandle(easy, OptionsOf(context, context.Argument2), multi);
    }

    private static List<(CurlOption Option, object? Value)> OptionsOf(LibTestContext context, string? url) =>
    [
        (CurlOption.Writedata, context.StandardOutput),
        (CurlOption.Url, url),
        (CurlOption.Verbose, 1L),
        (CurlOption.Failonerror, 1L),
    ];
}
