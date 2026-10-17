using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1903.c</c>: a GET of the case's URL reading cookies from <c>libtest_arg2</c>, then, unless it failed,
/// <c>curl_easy_reset</c> and the same GET again reading that file and writing the cookies to <c>libtest_arg3</c>
/// (<c>CURLOPT_COOKIEJAR</c>). The last perform's code is the program's.
/// </summary>
public sealed class Lib1903 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1903";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
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
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            if (!await steps.SetOptionsAndPerformAsync(
                    easy,
                    [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url), (CurlOption.Cookiefile, context.Argument2)],
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            easy.Reset();
            _ = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Cookiefile, context.Argument2),
                    (CurlOption.Cookiejar, context.Argument3),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
