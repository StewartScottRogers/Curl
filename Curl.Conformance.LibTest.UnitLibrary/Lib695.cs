using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib695.c</c>: rewinding a reused MIME structure - a verbose post, its body thrown away, of one part
/// <c>data</c>, <c>text/html</c>, <c>&lt;title&gt;hello&lt;/title&gt;</c>, then a second post whose one part has the
/// first structure as its subparts. A failing transfer writes <c>curl_easy_perform() 1 failed: &lt;text&gt;</c> (or
/// <c>2</c>) on stderr, and a refused <c>curl_mime_subparts</c> <c>curl_mime_subparts() failed: &lt;text&gt;</c>.
/// </summary>
public sealed class Lib695 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib695";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: the first post, then the post that reuses it.</summary>
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
            CurlMime first = library.MimeInit();
            using CurlMime second = library.MimeInit();
            bool reused = false;
            try
            {
                CurlMimePart part = first.AddPart();
                _ = part.SetData("<title>hello</title>");
                _ = part.SetType("text/html");
                _ = part.SetName("data");
                _ = steps.EasySetOptions(easy, [
                    (CurlOption.Url, context.Url),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite),
                    (CurlOption.Mimepost, first),
                ]);
                reused = await PerformOrWriteAsync(context, steps, easy, 1, cancellationToken).ConfigureAwait(false)
                    && Reuse(context, steps, easy, first, second);
                _ = reused && await PerformOrWriteAsync(context, steps, easy, 2, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Once it is the second structure's subparts, the first is freed with it.
                if (!reused)
                {
                    first.Dispose();
                }
            }
        }
    }

    /// <summary>Phase two: binds <paramref name="second"/>, then makes <paramref name="first"/> its one part's subparts.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="easy">The handle.</param>
    /// <param name="first">The structure posted first.</param>
    /// <param name="second">The new top-level structure.</param>
    /// <returns>Whether <c>curl_mime_subparts</c> succeeded; when not, its failure is written on stderr.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool Reuse(LibTestContext context, LibTestSteps steps, CurlEasy easy, CurlMime first, CurlMime second)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(easy);
        ArgumentNullException.ThrowIfNull(second);
        CurlMimePart part = second.AddPart();
        _ = easy.SetOption(CurlOption.Mimepost, second);
        steps.Result = part.SetSubParts(first);
        if (steps.Result == CurlCode.Ok)
        {
            return true;
        }

        context.StandardError.Write($"curl_mime_subparts() failed: {CurlErrorText.Easy(steps.Result)}\n");
        return false;
    }

    // One curl_easy_perform; a failing one written with its number and text.
    private static async ValueTask<bool> PerformOrWriteAsync(LibTestContext context, LibTestSteps steps, CurlEasy easy, int number, CancellationToken cancellationToken)
    {
        if (await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        context.StandardError.Write($"curl_easy_perform() {number} failed: {CurlErrorText.Easy(steps.Result)}\n");
        return false;
    }
}
