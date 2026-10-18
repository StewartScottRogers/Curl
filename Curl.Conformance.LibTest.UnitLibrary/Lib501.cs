using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib501.c</c>: a transfer with no URL set, <c>CURLOPT_HEADER</c> on and <c>CURLOPT_MAXREDIRS</c> -1, which
/// libcurl must refuse with <c>CURLE_URL_MALFORMAT</c> (3) rather than crash.
/// </summary>
public sealed class Lib501 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib501";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            await steps.SetOptionsAndPerformAsync(easy, [(CurlOption.Header, 1L), (CurlOption.Maxredirs, -1L)], cancellationToken).ConfigureAwait(false);
        }
    }
}
