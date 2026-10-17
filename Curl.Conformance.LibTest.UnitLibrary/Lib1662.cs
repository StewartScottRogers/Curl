using Curl.Api.Options.Http;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1662.c</c>: a verbose MIME post of one part named <c>content</c> with the file name <c>poetry.txt</c>, its
/// data <see cref="TestData"/> from a read callback of unknown size (-1) that gives one byte a call, following up to 50
/// redirects with <c>curl/2000</c> as the user agent. The perform's code is the program's; an easy handle that cannot
/// be created ends with <c>CURLE_OK</c>. Libcurl's <c>curl_mime_init</c> cannot fail, so its <c>if(mime1)</c> is left out.
/// </summary>
public sealed class Lib1662 : ILibTest
{
    /// <summary>The part's data.</summary>
    public const string TestData = "mooaaa";

    /// <inheritdoc/>
    public string ProgramName => "lib1662";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
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
        ArgumentNullException.ThrowIfNull(library);
        CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
        if (easy is null)
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            using CurlMime mime = library.MimeInit();
            CurlMimePart part = mime.AddPart();
            _ = part.SetDataCallback(-1, new Reader().Read, null);
            _ = part.SetFileName("poetry.txt");
            _ = part.SetName("content");
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Buffersize, 102400L);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Noprogress, 1L);
            _ = easy.SetOption(CurlOption.Mimepost, mime);
            _ = easy.SetOption(CurlOption.Useragent, "curl/2000");
            _ = easy.SetOption(CurlOption.Followlocation, 1L);
            _ = easy.SetOption(CurlOption.Maxredirs, 50L);
            _ = easy.SetOption(CurlOption.HttpVersion, (long)HttpVersion.Http2Tls);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = easy.SetOption(CurlOption.FtpSkipPasvIp, 1L);
            _ = easy.SetOption(CurlOption.TcpKeepalive, 1L);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>t1662_read_cb</c> over its <c>sizeleft</c>: <see cref="TestData"/> one byte a call.</summary>
    public sealed class Reader
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
