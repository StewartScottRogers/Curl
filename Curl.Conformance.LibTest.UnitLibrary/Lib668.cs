using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib668.c</c>: early end of part data detection - a verbose MIME post with headers on whose parts are
/// <c>field1</c>, <c>dummy</c> from a callback of known size 5, <c>field2</c>, <c>dummy</c> from a callback of
/// unknown size (so the post is chunked), and <c>field3</c>, the file <c>libtest_arg2</c>. A failing transfer
/// writes <c>curl_easy_perform() failed</c> on stderr.
/// </summary>
public sealed class Lib668 : ILibTest
{
    /// <summary><c>testdata</c>: the bytes each callback part gives.</summary>
    public const string TestData = "dummy";

    /// <inheritdoc/>
    public string ProgramName => "lib668";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: builds the three parts, then posts them.</summary>
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
            _ = part.SetName("field1");
            _ = part.SetDataCallback(TestData.Length, new ChunkReader().Read, null);
            part = mime.AddPart();
            _ = part.SetName("field2");
            _ = part.SetDataCallback(-1, new ChunkReader().Read, null);
            part = mime.AddPart();
            _ = part.SetName("field3");
            _ = part.SetFileData(context.Argument2);
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

    /// <summary><c>t668_read_cb</c>: gives as much of <see cref="TestData"/> as the buffer holds.</summary>
    public sealed class ChunkReader
    {
        private int position;

        /// <summary>Copies the next bytes, as many as fit, into <paramref name="buffer"/>.</summary>
        /// <param name="buffer">Where the bytes go.</param>
        /// <returns>The bytes copied, 0 once every byte was given.</returns>
        public long Read(Span<byte> buffer)
        {
            int length = Math.Min(buffer.Length, TestData.Length - position);
            for (int i = 0; i < length; i++)
            {
                buffer[i] = (byte)TestData[position++];
            }

            return length;
        }
    }
}
