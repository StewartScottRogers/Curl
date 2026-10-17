using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib666.c</c>: a huge binary-encoded MIME post - one part <c>upfile</c>, file name <c>myfile.txt</c>, 17000
/// bytes of the letters <c>A</c> to <c>Z</c> with a newline every 77th byte - sent verbose, with headers on and a
/// 16411-byte upload buffer. A failing part call writes <c>&lt;call&gt;() failed</c> on stderr and ends the program
/// with its code.
/// </summary>
public sealed class Lib666 : ILibTest
{
    private static readonly string[] PartCalls = ["curl_mime_name", "curl_mime_filename", "curl_mime_data", "curl_mime_encoder"];

    /// <inheritdoc/>
    public string ProgramName => "lib666";

    /// <summary><c>testbuf</c>: 17000 bytes, <c>A</c> to <c>Z</c> by position, with a newline at every position 76 modulo 77.</summary>
    /// <returns>A new copy of the bytes.</returns>
    public static byte[] TestBuffer() => [.. Enumerable.Range(0, 17000).Select(i => i % 77 == 76 ? (byte)'\n' : (byte)('A' + (i % 26)))];

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: builds the part, then posts it.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
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
            CurlMimePart part = mime.AddPart();
            steps.Result = LibTestMimeCalls.FirstFailure(
                [
                    () => part.SetName("upfile"),
                    () => part.SetFileName("myfile.txt"),
                    () => part.SetData(TestBuffer()),
                    () => part.SetEncoder("binary"),
                ],
                out int failed);
            if (steps.Result != CurlCode.Ok)
            {
                context.StandardError.Write($"{PartCalls[failed]}() failed\n");
                return;
            }

            _ = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Mimepost, mime),
                    (CurlOption.UploadBuffersize, 16411L),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Header, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
