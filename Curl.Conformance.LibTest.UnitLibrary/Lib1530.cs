using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1530.c</c>: a verbose GET of <see cref="Url"/> whose <c>CURLOPT_OPENSOCKETFUNCTION</c> writes
/// <c>opensocket() returns CURL_SOCKET_BAD</c> on stderr and gives no socket, so the transfer cannot connect; its
/// code is the program's. The case's URL is not used. A failing <c>curl_global_init</c> or <c>curl_easy_init</c>
/// writes its failure and ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib1530 : ILibTest
{
    /// <summary>The URL <c>lib1530.c</c> fetches.</summary>
    public const string Url = "http://99.99.99.99:9999";

    /// <inheritdoc/>
    public string ProgramName => "lib1530";

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
                    (CurlOption.Url, Url),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Opensocketfunction, (OpenSocketCallback)((_, _) => OpenNoSocket(context))),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>opensocket</c>: writes that it gives no socket, and gives none.</summary>
    /// <param name="context">The program's context.</param>
    /// <returns><see cref="CurlSocket.Bad"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static CurlSocket OpenNoSocket(LibTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.StandardError.Write("opensocket() returns CURL_SOCKET_BAD\n");
        return CurlSocket.Bad;
    }
}
