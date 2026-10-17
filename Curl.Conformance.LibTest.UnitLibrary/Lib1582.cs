using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1582.c</c>: a verbose request with headers authenticated by Negotiate with the empty credentials <c>:</c>,
/// the peer's certificate and host name not verified (case 1582: Negotiate over HTTPS with channel binding),
/// answering the perform's code.
/// </summary>
public sealed class Lib1582 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1582";

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
                    (CurlOption.Header, 1L),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Httpauth, (long)HttpAuthenticationMethods.Negotiate),
                    (CurlOption.Userpwd, ":"),
                    (CurlOption.SslVerifypeer, 0L),
                    (CurlOption.SslVerifyhost, 0L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
