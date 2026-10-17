using System.Globalization;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1485.c</c>: one GET whose write and header callbacks copy everything to stdout; at the blank line that ends a
/// response, the header callback reads <c>CURLINFO_RESPONSE_CODE</c> and, for a 2xx, <c>CURLINFO_CONTENT_LENGTH_DOWNLOAD_T</c>
/// while the transfer runs, writing both on stderr, and fails the transfer (<c>CURLE_WRITE_ERROR</c>) for a status outside
/// 100 to 999 or an unknown length. Its check of the length's own <c>curl_easy_getinfo</c> answer is left out: Libcurl
/// answers that info on a live handle without fail.
/// </summary>
public sealed class Lib1485 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1485";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>global_init</c>: the handle, its callbacks, the transfer.</summary>
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
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            var status = new TransferStatus(easy, context);
            await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Url, context.Url),
                    (CurlOption.Writefunction, (WriteCallback)status.Write),
                    (CurlOption.Headerfunction, (HeaderCallback)status.Header),
                    (CurlOption.Noprogress, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>struct t1485_transfer_status</c> with its two callbacks.</summary>
    /// <param name="easy">The handle the header callback reads its info from.</param>
    /// <param name="context">The program's context: its stdout and stderr.</param>
    public sealed class TransferStatus(CurlEasy easy, LibTestContext context)
    {
        /// <summary><c>CURLE_WRITE_ERROR</c>, which the header callback answers to fail the transfer.</summary>
        public const long WriteError = (long)CurlCode.WriteError;

        /// <summary><c>out_len</c>: the body bytes written.</summary>
        public long OutputLength { get; private set; }

        /// <summary><c>hd_line</c>: the header lines seen.</summary>
        public int HeaderLines { get; private set; }

        /// <summary><c>http_status</c>: the status of the last response that ended.</summary>
        public long HttpStatus { get; private set; }

        /// <summary><c>t1485_write_cb</c>: copies the body to stdout and counts it.</summary>
        /// <param name="data">The body bytes.</param>
        /// <returns>The byte count.</returns>
        public long Write(ReadOnlySpan<byte> data)
        {
            context.StandardOutput.Write(data);
            OutputLength += data.Length;
            return data.Length;
        }

        /// <summary><c>t1485_header_callback</c>: copies the header to stdout and checks each response's end.</summary>
        /// <param name="header">The header bytes.</param>
        /// <returns>The byte count, or <see cref="WriteError"/> to fail the transfer.</returns>
        public long Header(ReadOnlySpan<byte> header)
        {
            context.StandardOutput.Write(header);
            HeaderLines++;
            return header.SequenceEqual("\r\n"u8) ? EndOfResponse(header.Length) : header.Length;
        }

        private long EndOfResponse(long length)
        {
            CurlCode code = easy.GetInfo(CurlInfo.ResponseCode, out object? value);
            long status = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            context.StandardError.Write($"header_callback, get status: {status}, {(int)code}\n");
            if (status is < 100 or >= 1000)
            {
                context.StandardError.Write($"header_callback, invalid status: {status}, {(int)code}\n");
                return WriteError;
            }

            HttpStatus = status;
            return status is >= 200 and < 300 ? CheckContentLength(length) : length;
        }

        private long CheckContentLength(long length)
        {
            CurlCode code = easy.GetInfo(CurlInfo.ContentLengthDownloadT, out object? value);
            long contentLength = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            context.StandardError.Write($"header_callback, info Content-Length: {contentLength}, {(int)code}\n");
            if (contentLength >= 0)
            {
                return length;
            }

            context.StandardError.Write($"header_callback, expected known Content-Length, got: {contentLength}\n");
            return WriteError;
        }
    }
}
