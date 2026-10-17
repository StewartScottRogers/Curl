using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1908.c</c>: a GET of the case's URL with <c>libtest_arg2</c> as the <c>CURLOPT_ALTSVC</c> cache; when it succeeds,
/// a <c>curl_easy_duphandle</c> copy makes the same GET and is cleaned up first, then the first handle is reset and
/// cleaned up, so the cache file holds what the first handle saves. The last perform's code is the program's, and
/// <see cref="LibTestErrorCodes.MajorBad"/> when no easy handle can be created. <c>start_test_timing</c> is left out:
/// the program never checks the test's time.
/// </summary>
public sealed class Lib1908 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1908";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        steps.Result = LibTestErrorCodes.MajorBad;
        if (library.IsCleanedUp)
        {
            return;
        }

        CurlEasy easy = library.EasyInit();
        await using (easy.ConfigureAwait(false))
        {
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Noprogress, 1L);
            _ = easy.SetOption(CurlOption.Altsvc, context.Argument2);
            if (await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                CurlEasy copy = easy.DupHandle();
                await using (copy.ConfigureAwait(false))
                {
                    steps.Result = await copy.PerformAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            easy.Reset();
        }
    }
}
