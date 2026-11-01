using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Ends an HTTP transfer early with the curl exit code and message the transfer reports.
/// Thrown while a response is read and caught by the handler that turns it into a
/// <see cref="TransferResult" />; it never leaves this library.
/// </summary>
/// <param name="exitCode">The curl exit code the transfer reports.</param>
/// <param name="message">The message curl prints for the failure.</param>
internal sealed class HttpTransferException(CurlExitCode exitCode, string message)
    : Exception(message)
{
    /// <summary>
    /// Gets the curl exit code the transfer reports.
    /// </summary>
    internal CurlExitCode ExitCode { get; } = exitCode;

    /// <summary>
    /// Gets a value indicating whether the server refused the request's stream, so the handler
    /// may send it again on a new connection: an HTTP/3 stream reset with
    /// <c>H3_REQUEST_REJECTED</c> (ADR-0187), or an HTTP/2 stream reset with
    /// <c>REFUSED_STREAM</c> or left unprocessed by a GOAWAY (BL-3560). Any <c>-v</c> line that
    /// reports the refusal is among <see cref="InfoLines" />.
    /// </summary>
    internal bool IsStreamRefused { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server reset the request's HTTP/2 stream with
    /// <c>HTTP_1_1_REQUIRED</c>, so the handler sends it again over HTTP/1.1 on a new
    /// connection, as curl 8.18.0 with nghttp2 does (CGR-032).
    /// </summary>
    internal bool RequiresHttp11 { get; init; }

    /// <summary>
    /// Gets a value indicating whether the failure ended only the request's HTTP/2 stream - the
    /// server reset it - so its connection stays open for the next transfer, as curl 8.18.0 with
    /// nghttp2 leaves a multiplexed connection intact after one stream fails (CGR-140).
    /// </summary>
    internal bool LeavesConnectionIntact { get; init; }

    /// <summary>
    /// Gets the <c>-v</c> lines the handler reports for the failure, in order, once the head
    /// lines it holds are reported; none by default. Too many response headers reports its
    /// message, and among a chunked body's trailers also
    /// <see cref="HttpTransferMessages.ChunkedStreamReadFailed" /> after it (measured, BL-1448 Notes).
    /// </summary>
    internal IReadOnlyList<string> InfoLines { get; init; } = [];

    /// <summary>
    /// Gets the head bytes accepted before the failure, which the handler writes to the header
    /// output before it fails, as libcurl 8.22.0 writes each header line once it accepts it
    /// (upstream test580, BL-2587); none by default. The head reader sets it on a failure among
    /// the header lines or after a 1xx head (upstream tests 158 and 415, BL-2867).
    /// </summary>
    internal byte[] HeadBytesBeforeFailure { get; set; } = [];

    /// <summary>
    /// Gets or sets the status line of the head the failure cut short, or <see langword="null" /> when it came
    /// before one: libcurl 8.22.0 still reports that response's code and version, and the bytes of
    /// <see cref="HeadBytesBeforeFailure" /> as its header size, after a failure among its header lines (BL-3195).
    /// </summary>
    internal HttpStatusLine? StatusLineBeforeFailure { get; set; }

    /// <summary>
    /// Gets or sets how many response headers the transfer had stored when the failure cut the head short, counting the
    /// whole header lines of the failed head, which curl 8.18.0 has already stored and so counts in <c>%{num_headers}</c>
    /// (BL-3979); zero by default.
    /// </summary>
    internal int HeadersStoredBeforeFailure { get; set; }

    /// <summary>
    /// Gets or sets the <c>Content-Type</c> of the 1xx head a failure came after, which libcurl 8.22.0 still reports as
    /// <c>CURLINFO_CONTENT_TYPE</c> (BL-3301); <see langword="null" /> by default.
    /// </summary>
    internal string? ContentTypeBeforeFailure { get; set; }

    /// <summary>
    /// Gets the operating system's error number of the socket error that failed a receive, the
    /// transfer's <c>CURLINFO_OS_ERRNO</c> as libcurl 8.22.0 sets it after a failed receive on a kept
    /// connection (BL-3329): the Winsock code on Windows (10054 or 10053), <c>errno</c> elsewhere;
    /// <c>0</c> when no socket error failed it.
    /// </summary>
    internal long OsErrno { get; init; }

    /// <summary>
    /// Builds the exit 56 failure of a receive that threw <paramref name="exception" />, carrying
    /// the message <see cref="HttpTransferMessages.ReceiveFailure" /> words and the socket error's
    /// operating system error number.
    /// </summary>
    /// <param name="exception">The failure the receive threw.</param>
    /// <returns>The failure.</returns>
    internal static HttpTransferException OfReceiveFailure(IOException exception)
    {
        Exception? current = exception;
        while (current is not null and not SocketException)
        {
            current = current.InnerException;
        }

        return new HttpTransferException(CurlExitCode.RecvError, HttpTransferMessages.ReceiveFailure(exception))
        {
            OsErrno = (current as SocketException)?.NativeErrorCode ?? 0,
        };
    }
}
