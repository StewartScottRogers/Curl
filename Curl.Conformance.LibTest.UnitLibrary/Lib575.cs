using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib575.c</c>: a verbose FTP wildcard download three times: performed twice on one handle, then once more on a
/// <c>curl_easy_duphandle</c> of it through a multi handle's <c>select()</c> loop of 100 ms, the original cleaned up
/// first. Cleanup type UA: the multi handle is cleaned up with the duplicate still in it. The program answers the
/// first failing step's code.
/// </summary>
/// <param name="timeProvider">The program's clock, for <c>abort_on_test_timeout</c>.</param>
public sealed class Lib575(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib575";

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
    /// <param name="clock">The program's clock, started.</param>
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

        CurlEasy duplicate;
        await using (easy.ConfigureAwait(false))
        {
            if (!await steps.SetOptionsAndPerformAsync(
                    easy,
                    [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url), (CurlOption.Wildcardmatch, 1L), (CurlOption.Verbose, 1L)],
                    cancellationToken).ConfigureAwait(false)
                || !await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            duplicate = easy.DupHandle();
        }

        await using (duplicate.ConfigureAwait(false))
        {
            if (steps.MultiInit(library, out CurlMulti? multi))
            {
                await using (multi.ConfigureAwait(false))
                {
                    _ = steps.MultiAddHandle(multi, duplicate)
                        && await steps.PerformUntilDoneAsync(multi, clock, 100, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
