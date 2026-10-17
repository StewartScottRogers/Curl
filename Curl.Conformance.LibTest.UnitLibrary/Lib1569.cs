using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1569.c</c>: a verbose transfer with headers of the case's URL, then, unless it failed, the same handle
/// performed again for <c>libtest_arg2</c> (cases 1569 and 1570: an FTP URL with <c>;type=A</c> or <c>;type=D</c>,
/// then the same URL without it). The last perform's code is the program's.
/// </summary>
public sealed class Lib1569 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1569";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
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
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            if (!await steps.SetOptionsAndPerformAsync(
                    easy,
                    [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url), (CurlOption.Verbose, 1L), (CurlOption.Header, 1L)],
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            _ = easy.SetOption(CurlOption.Url, context.Argument2);
            _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
        }
    }
}
