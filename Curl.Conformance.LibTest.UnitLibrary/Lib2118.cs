using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2118.c</c>: a verbose transfer of the case's URL over IPv4 with <c>CURLOPT_SSL_VERIFYPEER</c> off and
/// <c>CURLOPT_SSL_VERIFYHOST</c> on, so a certificate issued for another name is still rejected. The transfer's code is
/// the program's. A failing <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure and ends with
/// <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib2118 : ILibTest
{
    /// <summary><c>CURL_IPRESOLVE_V4</c>.</summary>
    public const long IpResolveV4 = 1L;

    /// <inheritdoc/>
    public string ProgramName => "lib2118";

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
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Ipresolve, IpResolveV4),
                    (CurlOption.SslVerifypeer, 0L),
                    (CurlOption.SslVerifyhost, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
