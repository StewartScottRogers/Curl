using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib757.c</c>: a MIME structure whose one part <c>data</c> (<c>text/html</c>) reads <c>&lt;title&gt;fun-times&lt;/title&gt;</c>
/// from a callback is posted verbose, then reused as the subparts of a second structure's part and posted again,
/// so the callback part must rewind. The body is thrown away; a failing step writes its line on stderr.
/// </summary>
public sealed class Lib757 : ILibTest
{
    /// <summary><c>t757_data</c>: the part's content.</summary>
    public static ReadOnlySpan<byte> Data => "<title>fun-times</title>"u8;

    /// <inheritdoc/>
    public string ProgramName => "lib757";

    /// <summary><c>read_757</c>: the whole of <see cref="Data"/> when it fits in <paramref name="buffer"/>, else nothing.</summary>
    /// <param name="buffer">Where the content goes.</param>
    /// <returns>How many bytes were copied.</returns>
    public static long Read(Span<byte> buffer)
    {
        if (buffer.Length < Data.Length)
        {
            return 0;
        }

        Data.CopyTo(buffer);
        return Data.Length;
    }

    /// <summary><c>seek_757</c>: every seek succeeds.</summary>
    /// <param name="offset">Ignored.</param>
    /// <param name="origin">Ignored.</param>
    /// <returns><see cref="SeekResult.Ok"/>.</returns>
    public static SeekResult Seek(long offset, SeekOrigin origin) => SeekResult.Ok;

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: the first post, then the post of the structure reused as a child.</summary>
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
            CurlMime? first = library.MimeInit();
            using CurlMime second = library.MimeInit();
            try
            {
                CurlMimePart part = first.AddPart();
                _ = part.SetDataCallback(Data.Length, Read, Seek);
                _ = part.SetType("text/html");
                _ = part.SetName("data");
                _ = steps.EasySetOptions(easy, [(CurlOption.Url, context.Url), (CurlOption.Verbose, 1L), (CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite), (CurlOption.Mimepost, first)]);
                if (await PerformOrReportAsync(context, steps, easy, 1, cancellationToken).ConfigureAwait(false) && ReuseAsChild(context, steps, easy, first, second))
                {
                    first = null;
                    _ = await PerformOrReportAsync(context, steps, easy, 2, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                first?.Dispose();
            }
        }
    }

    // Phase two: a part of the second structure takes the first as its subparts.
    private static bool ReuseAsChild(LibTestContext context, LibTestSteps steps, CurlEasy easy, CurlMime first, CurlMime second)
    {
        CurlMimePart part = second.AddPart();
        _ = steps.EasySetOption(easy, CurlOption.Mimepost, second);
        steps.Result = part.SetSubParts(first);
        if (steps.Result == CurlCode.Ok)
        {
            return true;
        }

        context.StandardError.Write($"curl_mime_subparts() failed: {CurlErrorText.Easy(steps.Result)}\n");
        return false;
    }

    private static async ValueTask<bool> PerformOrReportAsync(LibTestContext context, LibTestSteps steps, CurlEasy easy, int number, CancellationToken cancellationToken)
    {
        if (await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        context.StandardError.Write($"curl_easy_perform() {number} failed: {CurlErrorText.Easy(steps.Result)}\n");
        return false;
    }
}
