using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib652.c</c>: a verbose SMTP mail from <c>somebody@example.com</c> to <c>someone@example.com</c> whose one
/// MIME part is <c>myfile.jpg</c>, <c>image/jpeg</c>, 17000 bytes of ten-letter runs <c>A</c> to <c>Z</c>, base64
/// encoded, sent with a 16411-byte upload buffer. A failing part call writes <c>&lt;call&gt;() failed</c> on
/// stderr and ends the program with its code.
/// </summary>
public sealed class Lib652 : ILibTest
{
    private static readonly string[] PartCalls = ["curl_mime_filename", "curl_mime_type", "curl_mime_data", "curl_mime_encoder"];

    /// <inheritdoc/>
    public string ProgramName => "lib652";

    /// <summary><c>testbuf</c>: 1700 runs of ten letters, <c>A</c> to <c>Z</c> over and over.</summary>
    /// <returns>A new copy of the 17000 bytes.</returns>
    public static byte[] TestBuffer() => [.. Enumerable.Range(0, 17000).Select(i => (byte)('A' + (i / 10 % 26)))];

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: builds the part, then sends the mail.</summary>
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
                    () => part.SetFileName("myfile.jpg"),
                    () => part.SetType("image/jpeg"),
                    () => part.SetData(TestBuffer()),
                    () => part.SetEncoder("base64"),
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
                    (CurlOption.MailFrom, "somebody@example.com"),
                    (CurlOption.MailRcpt, CurlSlist.Append(null, "someone@example.com")),
                    (CurlOption.Mimepost, mime),
                    (CurlOption.UploadBuffersize, 16411L),
                    (CurlOption.Verbose, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
