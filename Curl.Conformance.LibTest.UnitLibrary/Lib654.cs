using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib654.c</c>: a three-part <c>CURLOPT_MIMEPOST</c> - a base64 <c>greeting</c> with its own header, the file
/// <c>libtest_arg2</c> and <c>dummy\n</c> from a read callback of unknown size - bound to a verbose handle with
/// headers on, which is then duplicated before the structure is freed. The original handle then posts
/// nothing and the duplicate posts its own copy; a failing perform writes
/// <c>curl_easy_perform(original) failed</c> or <c>curl_easy_perform(duplicated) failed</c> on stderr. Upstream
/// also counts the part's <c>freefunc</c> calls, which Libcurl's <c>curl_mime_data_cb</c> has no counterpart for,
/// so that check is left out.
/// </summary>
public sealed class Lib654 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib654";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: binds, duplicates, frees, then performs both handles.</summary>
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
            using CurlMime mime = library.MimeInit();
            BuildParts(mime, context.Argument2);
            _ = steps.EasySetOptions(easy, [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, context.Url),
                (CurlOption.Verbose, 1L),
                (CurlOption.Header, 1L),
                (CurlOption.Mimepost, mime),
            ]);
            await using CurlEasy duplicate = easy.DupHandle();
            mime.Dispose();
            _ = await PerformAsync(context, steps, easy, "original", cancellationToken).ConfigureAwait(false)
                && await PerformAsync(context, steps, duplicate, "duplicated", cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The three parts: the encoded greeting, the file and the callback.</summary>
    /// <param name="mime">The structure to add them to.</param>
    /// <param name="file">The file part's path, <c>libtest_arg2</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mime"/> is <see langword="null"/>.</exception>
    public static void BuildParts(CurlMime mime, string? file)
    {
        ArgumentNullException.ThrowIfNull(mime);
        CurlMimePart greeting = mime.AddPart();
        greeting.SetData("hello");
        greeting.SetName("greeting");
        greeting.SetType("application/X-Greeting");
        greeting.SetEncoder("base64");
        greeting.SetHeaders(["X-Test-Number: 654"]);
        mime.AddPart().SetFileData(file);
        mime.AddPart().SetDataCallback(-1, new Lib643.ByteReader().Read, null);
    }

    // One curl_easy_perform; a failing one is named on stderr.
    private static async ValueTask<bool> PerformAsync(LibTestContext context, LibTestSteps steps, CurlEasy easy, string which, CancellationToken cancellationToken)
    {
        if (await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        context.StandardError.Write($"curl_easy_perform({which}) failed\n");
        return false;
    }
}
