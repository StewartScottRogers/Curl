using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib676.c</c>: two verbose transfers with headers on and the user agent <c>the-moo agent next generation</c>,
/// the first reading cookies from <c>libtest_arg2</c>, the second after <c>CURLOPT_COOKIEFILE</c> is set to
/// <see langword="null"/>, which clears them. A failing transfer writes <c>retrieve 1 failed</c> or
/// <c>retrieve 2 failed</c> on stderr.
/// </summary>
public sealed class Lib676 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib676";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: the transfer with cookies, then the one without.</summary>
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
            _ = steps.EasySetOptions(easy, [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, context.Url),
                (CurlOption.Header, 1L),
                (CurlOption.Useragent, "the-moo agent next generation"),
                (CurlOption.Cookiefile, context.Argument2),
                (CurlOption.Verbose, 1L),
            ]);
            if (await steps.EasyPerformOrWriteAsync(easy, "retrieve 1 failed", cancellationToken).ConfigureAwait(false))
            {
                _ = steps.EasySetOption(easy, CurlOption.Cookiefile, null);
                _ = await steps.EasyPerformOrWriteAsync(easy, "retrieve 2 failed", cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
