using Curl.Api.Options.Protocols;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib567.c</c>: one verbose RTSP <c>OPTIONS</c> request of the case's URL, which is also the stream URI, with the
/// user agent <c>test567</c> and the custom header <c>Test-Number: 567</c>, headers and body to stdout. The program
/// answers the transfer's code.
/// </summary>
public sealed class Lib567 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib567";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
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
            CurlSlist headers = CurlSlist.Append(null, "Test-Number: 567");
            await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Headerdata, context.StandardOutput),
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Url, context.Url),
                    (CurlOption.RtspStreamUri, context.Url),
                    (CurlOption.RtspRequest, (long)RtspRequest.Options),
                    (CurlOption.Useragent, "test567"),
                    (CurlOption.Rtspheader, headers),
                ],
                cancellationToken).ConfigureAwait(false);
            CurlSlist.FreeAll(headers);
        }
    }
}
