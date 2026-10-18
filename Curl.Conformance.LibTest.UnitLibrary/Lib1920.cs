using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1920.c</c>: a verbose GET of the case's URL reading and writing cookies in <c>libtest_arg2</c>
/// (<c>CURLOPT_COOKIEFILE</c> and <c>CURLOPT_COOKIEJAR</c>), then, unless it failed, <c>curl_easy_reset</c> and the
/// cookie jar set again, so the cleanup still writes the jar and the reset must not have emptied it. The perform's
/// code is the program's; an easy handle that cannot be created ends with <c>CURLE_OK</c>.
/// </summary>
public sealed class Lib1920 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1920";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        if (library.IsCleanedUp)
        {
            return;
        }

        CurlEasy easy = library.EasyInit();
        await using (easy.ConfigureAwait(false))
        {
            if (await steps.SetOptionsAndPerformAsync(
                    easy,
                    [
                        (CurlOption.Writedata, context.StandardOutput),
                        (CurlOption.Cookiefile, context.Argument2),
                        (CurlOption.Cookiejar, context.Argument2),
                        (CurlOption.Url, context.Url),
                        (CurlOption.Verbose, 1L),
                    ],
                    cancellationToken).ConfigureAwait(false))
            {
                easy.Reset();
                _ = steps.EasySetOption(easy, CurlOption.Cookiejar, context.Argument2);
            }
        }
    }
}
