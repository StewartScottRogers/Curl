using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1972.c</c>: <see cref="LibAwsSigV4S3Request"/>'s signed request with a MIME body of one part, <c>foo</c> holding
/// <c>bar</c>, sent with <c>CURLOPT_MIMEPOST</c> and the <c>Content-Type: application/json</c> header. The transfer's
/// code is the program's. A failing <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure and ends with
/// <see cref="LibTestErrorCodes.MajorBad"/>; the <c>curl_mime_init</c> and <c>curl_mime_addpart</c> checks are left out:
/// Libcurl answers both on a live library.
/// </summary>
public sealed class Lib1972 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1972";

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

        using CurlMime mime = library.MimeInit();
        await using (easy.ConfigureAwait(false))
        {
            CurlMimePart part = mime.AddPart();
            _ = part.SetName("foo");
            _ = part.SetData("bar");
            List<(CurlOption Option, object? Value)> options = [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Mimepost, mime)];
            options.AddRange(LibAwsSigV4S3Request.SignedRequestOptions(context, [LibAwsSigV4S3Request.JsonContentType]));
            _ = await steps.SetOptionsAndPerformAsync(easy, options, cancellationToken).ConfigureAwait(false);
        }
    }
}
