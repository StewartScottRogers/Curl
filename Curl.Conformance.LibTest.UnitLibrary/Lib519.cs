using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib519.c</c>: two transfers on one handle, the first as <c>monster:underbed</c> and, when it succeeds,
/// the second as <c>anothermonster:inwardrobe</c> (<c>CURLOPT_USERPWD</c>), verbose and headers on.
/// </summary>
public sealed class Lib519 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib519";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
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
            bool first = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Userpwd, "monster:underbed"),
                    (CurlOption.Header, 1L),
                    (CurlOption.Verbose, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
            if (first)
            {
                await steps.SetOptionsAndPerformAsync(easy, [(CurlOption.Userpwd, "anothermonster:inwardrobe")], cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
