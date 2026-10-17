using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib589.c</c>, which case 584 runs too: verbose with headers on, a zero-byte POST after
/// <c>CURLOPT_MIMEPOST</c> is set to <see langword="null"/>. For case 584 a MIME POST of one part
/// <c>fake</c> = <c>party</c> goes first, and a failing one ends the program with its code.
/// </summary>
public sealed class Lib589 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib589";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: <c>curl_easy_init</c>, the options and the one or two performs.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
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
            _ = steps.EasySetOptions(easy, [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, context.Url),
                (CurlOption.Verbose, 1L),
                (CurlOption.Header, 1L),
            ]);
            using CurlMime? mime = context.TestNumber == 584 ? library.MimeInit() : null;
            if (mime is null || await PerformMimePostAsync(steps, easy, mime, cancellationToken).ConfigureAwait(false))
            {
                _ = steps.EasySetOption(easy, CurlOption.Mimepost, null);
                _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Case 584's first transfer: the one-part MIME POST.</summary>
    /// <param name="steps">The program's steps.</param>
    /// <param name="easy">The handle.</param>
    /// <param name="mime">The MIME structure to fill and post.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>Whether it succeeded, so the program goes on.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<bool> PerformMimePostAsync(LibTestSteps steps, CurlEasy easy, CurlMime mime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(mime);
        CurlMimePart part = mime.AddPart();
        part.SetName("fake");
        part.SetData("party");
        _ = steps.EasySetOption(easy, CurlOption.Mimepost, mime);
        return await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
    }
}
