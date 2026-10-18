using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib667.c</c>: a verbose MIME post with headers on whose one part <c>field</c> is base64 encoded and read
/// from a callback of unknown size that gives <c>dummy</c> one byte a call, so the post is chunked. A failing
/// transfer writes <c>curl_easy_perform() failed</c> on stderr.
/// </summary>
public sealed class Lib667 : ILibTest
{
    /// <summary><c>testdata</c>: the bytes the part's callback gives.</summary>
    public const string TestData = "dummy";

    /// <inheritdoc/>
    public string ProgramName => "lib667";

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
            _ = part.SetName("field");
            _ = part.SetEncoder("base64");
            _ = part.SetDataCallback(-1, new ByteReader().Read, null);
            _ = steps.EasySetOptions(easy, [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, context.Url),
                (CurlOption.Verbose, 1L),
                (CurlOption.Header, 1L),
                (CurlOption.Mimepost, mime),
            ]);
            _ = await steps.EasyPerformOrWriteAsync(easy, "curl_easy_perform() failed", cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>t667_read_cb</c>: gives <see cref="TestData"/> one byte a call.</summary>
    public sealed class ByteReader
    {
        private int position;

        /// <summary>Copies the next byte, if any, into <paramref name="buffer"/>.</summary>
        /// <param name="buffer">Where the byte goes.</param>
        /// <returns>1, or 0 for an empty buffer or once every byte was given.</returns>
        public long Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty || position == TestData.Length)
            {
                return 0;
            }

            buffer[0] = (byte)TestData[position++];
            return 1;
        }
    }
}
