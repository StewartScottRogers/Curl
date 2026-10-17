using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib598.c</c>: a verbose download with headers on, a referer, a user agent and a cookie, then
/// <c>curl_easy_reset</c> and the same download with only the URL, headers and verbose set again. A failing
/// download writes <c>retrieve 1 failed</c> or <c>retrieve 2 failed</c> on stderr and ends with its code.
/// </summary>
public sealed class Lib598 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib598";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: <c>curl_easy_init</c>, the two downloads around the reset.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            if (!await RetrieveAsync(context, steps, easy, 1, [
                (CurlOption.Referer, "http://example.com/the-moo"),
                (CurlOption.Useragent, "the-moo agent next generation"),
                (CurlOption.Cookie, "name=moo"),
            ], cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            easy.Reset();
            _ = await RetrieveAsync(context, steps, easy, 2, [], cancellationToken).ConfigureAwait(false);
        }
    }

    // One download: the program's stdout, the URL, headers on, the extra options, verbose, then the perform.
    private static async ValueTask<bool> RetrieveAsync(
        LibTestContext context,
        LibTestSteps steps,
        CurlEasy easy,
        int number,
        IEnumerable<(CurlOption Option, object? Value)> extraOptions,
        CancellationToken cancellationToken)
    {
        bool succeeded = await steps.SetOptionsAndPerformAsync(
            easy,
            [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, context.Url),
                (CurlOption.Header, 1L),
                .. extraOptions,
                (CurlOption.Verbose, 1L),
            ],
            cancellationToken).ConfigureAwait(false);
        if (!succeeded)
        {
            context.StandardError.Write($"retrieve {number} failed\n");
        }

        return succeeded;
    }
}
