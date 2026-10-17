using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1510.c</c>: one verbose easy handle with headers, allowed three connections (<c>CURLOPT_MAXCONNECTS</c>),
/// performs <see cref="UrlCount"/> URLs in turn, each to its own <c>serverN.example.com</c> resolved to the test server
/// (<see cref="LibTestServerResolves"/>), so the oldest connection is the one closed; it stops at the first transfer
/// that fails. Its checks of the options set before the loop are left out: Libcurl takes these values without fail.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from.</param>
public sealed class Lib1510(TimeProvider timeProvider) : ILibTest
{
    /// <summary><c>NUM_URLS</c>: the URLs the program performs.</summary>
    public const int UrlCount = 4;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1510";

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        CurlSlist? resolve = LibTestServerResolves.AppendAndWrite(context, UrlCount);
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        LibTestResult result = await LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, resolve, cancellationToken)).ConfigureAwait(false);
        CurlSlist.FreeAll(resolve);
        return result;
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="resolve">The <c>CURLOPT_RESOLVE</c> list.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, CurlSlist? resolve, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = steps.EasySetOptions(easy, [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Verbose, 1L),
                (CurlOption.Header, 1L),
                (CurlOption.Resolve, resolve),
                (CurlOption.Maxconnects, 3L),
            ]);

            for (int n = 1; n <= UrlCount; n++)
            {
                if (!await steps.SetOptionsAndPerformAsync(easy, [(CurlOption.Url, LibTestServerResolves.Url(context, n))], cancellationToken).ConfigureAwait(false)
                    || !steps.AbortOnTestTimeout(clock))
                {
                    return;
                }
            }
        }
    }
}
