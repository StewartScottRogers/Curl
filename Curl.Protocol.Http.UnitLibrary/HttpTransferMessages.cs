using System.Globalization;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Every failure message sending an HTTP/1.x or HTTP/2 request body or reading a response head or
/// body reports, as curl 8.21.0 prints it. Each was measured against a loopback server
/// (BL-169, BL-170, BL-171, BL-174, BL-175, BL-176, BL-178, BL-180, BL-658) except <see cref="ReceiveFailed" /> and
/// <see cref="SendFailed" />, which are the texts <c>curl_easy_strerror</c> gives exits 56 and
/// 55.
/// </summary>
internal static class HttpTransferMessages
{
    /// <summary>
    /// The exit 1 message for a response whose first bytes cannot begin <c>HTTP/</c>.
    /// </summary>
    internal const string Http09NotAllowed = "Received HTTP/0.9 when not allowed";

    /// <summary>
    /// The exit 1 message for an <c>HTTP/1</c> status line that is not <c>HTTP/1.0</c> or
    /// <c>HTTP/1.1</c> followed by a blank and three digits.
    /// </summary>
    internal const string UnsupportedHttp1Subversion = "Unsupported HTTP/1 subversion in response";

    /// <summary>
    /// The exit 1 message for an <c>HTTP/</c> status line naming a major version other than
    /// 1, 2 or 3.
    /// </summary>
    internal const string UnsupportedHttpVersion = "Unsupported HTTP version in response";

    /// <summary>
    /// The exit 1 message for a status code below 100.
    /// </summary>
    internal const string UnsupportedResponseCode = "Unsupported response code in HTTP response";

    /// <summary>
    /// The exit 8 message for a header line with no colon, or a continuation line with no
    /// header before it.
    /// </summary>
    internal const string HeaderWithoutColon = "Header without colon";

    /// <summary>
    /// The exit 8 message for a carriage return inside a head line rather than before its
    /// line feed.
    /// </summary>
    internal const string CarriageReturnInHeader = "Carriage return found in header";

    /// <summary>
    /// The exit 8 message for a Content-Length header that is not a list of equal decimal
    /// numbers, or that disagrees with another Content-Length header.
    /// </summary>
    internal const string InvalidContentLength = "Invalid Content-Length: value";

    /// <summary>
    /// The exit 52 message for a peer that closed before a final status line arrived.
    /// </summary>
    internal const string EmptyReply = "Empty reply from server";

    /// <summary>
    /// The exit 56 message for a read the peer reset.
    /// </summary>
    internal const string ConnectionReset = "Recv failure: Connection was reset";

    /// <summary>
    /// The exit 56 message curl falls back to for any other failed read.
    /// </summary>
    internal const string ReceiveFailed = "Failure when receiving data from the peer";

    /// <summary>
    /// The exit 16 message for an HTTP/2 connection the peer closed before a response head
    /// arrived (measured, BL-658 Notes).
    /// </summary>
    internal const string Http2FramingError = "Error in the HTTP2 framing layer";

    /// <summary>
    /// The exit 18 message for an HTTP/2 connection the peer closed part way through a
    /// response body (measured, BL-658 Notes).
    /// </summary>
    internal const string PartialFile = "Transferred a partial file";

    /// <summary>
    /// Formats the exit 92 message for an HTTP/2 stream the peer reset, or that this client
    /// reset for a malformed response: <c>HTTP/2 stream 1 was not closed cleanly:
    /// INTERNAL_ERROR (err 2)</c> (measured, BL-658 Notes).
    /// </summary>
    /// <param name="streamId">The stream.</param>
    /// <param name="errorCode">The RST_STREAM's error code.</param>
    /// <returns>The message.</returns>
    internal static string Http2StreamNotClosedCleanly(int streamId, Curl.Http2.Http2ErrorCode errorCode) =>
        string.Create(CultureInfo.InvariantCulture, $"HTTP/2 stream {streamId} was not closed cleanly: {Http2ErrorName(errorCode)} (err {(uint)errorCode})");

    /// <summary>
    /// Formats the exit 16 message for an HTTP/2 connection ended by a protocol error:
    /// <c>nghttp2 shuts down connection with error 1: PROTOCOL_ERROR</c> (measured, BL-658 Notes).
    /// </summary>
    /// <param name="errorCode">The error code of the GOAWAY sent.</param>
    /// <returns>The message.</returns>
    internal static string Http2ShutsDownConnection(Curl.Http2.Http2ErrorCode errorCode) =>
        string.Create(CultureInfo.InvariantCulture, $"nghttp2 shuts down connection with error {(uint)errorCode}: {Http2ErrorName(errorCode)}");

    /// <summary>
    /// The exit 3 message for <c>--http3-only</c> with a URL that is not <c>https://</c>
    /// (measured, ADR-0144).
    /// </summary>
    internal const string Http3NeedsHttps = "HTTP/3 requested for non-HTTPS URL";

    /// <summary>
    /// Why <c>--http3</c> or <c>--http3-only</c> gives up HTTP/3 for an <c>https://</c> URL
    /// through a SOCKS proxy (measured on curl.se's 8.18.0 build, BL-837; unchanged in
    /// 8.21.0, ADR-0187).
    /// </summary>
    internal const string Http3NotOverSocksProxy = "HTTP/3 is not supported over a SOCKS proxy";

    /// <summary>
    /// Why <c>--http3</c> or <c>--http3-only</c> gives up HTTP/3 for an <c>https://</c> URL
    /// through an HTTP or HTTPS proxy (measured on curl.se's 8.18.0 build, BL-837, ADR-0223).
    /// </summary>
    internal const string Http3NotOverHttpProxy = "HTTP/3 is not supported over an HTTP proxy";

    /// <summary>
    /// Why <c>--http3-only</c>, or <c>--http3</c> with an <c>https://</c> URL, gives up HTTP/3
    /// over a Unix domain socket: <c>Curl_conn_may_http3</c> in <c>lib/vquic/vquic.c</c> at
    /// <c>curl-8_21_0</c> (ADR-0187, BL-867), exit 96 for <c>--http3-only</c>.
    /// </summary>
    internal const string Http3NotOverUnixSocket = "HTTP/3 cannot be used over UNIX domain sockets";

    /// <summary>
    /// Formats the message for an HTTP/3 request stream the server reset, exit 95 before any
    /// body byte arrived and exit 18 after: <c>HTTP/3 stream 0 reset by server (error 0x10c
    /// REQUEST_CANCELLED)</c> (<c>cf-ngtcp2.c</c> at <c>curl-8_21_0</c>, ADR-0187).
    /// </summary>
    /// <param name="streamId">The QUIC stream ID.</param>
    /// <param name="errorCode">The application error code the reset carried.</param>
    /// <returns>The message.</returns>
    internal static string Http3StreamReset(long streamId, long errorCode) =>
        string.Create(CultureInfo.InvariantCulture, $"HTTP/3 stream {streamId} reset by server (error 0x{errorCode:x} {Http3ErrorName(errorCode)})");

    /// <summary>
    /// The exit 55 message for an HTTP/3 request stream the QUIC connection cannot open, as
    /// <c>h3_stream_open</c> in <c>cf-ngtcp2.c</c> at <c>curl-8_21_0</c> reports a failing
    /// <c>ngtcp2_conn_open_bidi_stream</c> (ADR-0187, ADR-0245).
    /// </summary>
    internal const string Http3CannotOpenBidiStreams = "cannot open bidi streams";

    /// <summary>
    /// Formats the <c>-v</c> line for an HTTP/3 request stream the server reset with
    /// <c>H3_REQUEST_REJECTED</c>: <c>HTTP/3 stream 0 refused by server, try again on a new
    /// connection</c> (<c>cf-ngtcp2.c</c> at <c>curl-8_21_0</c>, ADR-0187).
    /// </summary>
    /// <param name="streamId">The QUIC stream ID.</param>
    /// <returns>The line.</returns>
    internal static string Http3StreamRefused(long streamId) =>
        string.Create(CultureInfo.InvariantCulture, $"HTTP/3 stream {streamId} refused by server, try again on a new connection");

    /// <summary>
    /// Formats the exit 56 message for a request refused once more after curl's own retries on
    /// a new connection ran out: <c>Connection died, tried 5 times before giving up</c>
    /// (<c>Curl_retry_request</c>, ADR-0187).
    /// </summary>
    /// <param name="retries">The retries run.</param>
    /// <returns>The message.</returns>
    internal static string ConnectionDiedGivingUp(int retries) =>
        string.Create(CultureInfo.InvariantCulture, $"Connection died, tried {retries} times before giving up");

    /// <summary>
    /// The exit 65 message for a <c>-T</c> upload that must be sent again but cannot seek back to
    /// its start, such as stdin: curl 8.21.0's <c>cr_in_rewind</c> (<c>lib/sendf.c</c>) fails with
    /// it when the tool's seek callback answers <c>CURL_SEEKFUNC_CANTSEEK</c>, which is 2 (ADR-0279).
    /// </summary>
    internal const string UploadSeekFailed = "seek callback returned error 2";

    /// <summary>
    /// The <c>-v</c> line curl 8.21.0's <c>Curl_client_start</c> (<c>lib/sendf.c</c>) writes after
    /// <see cref="UploadSeekFailed" />, naming its file reader and exit 65 (ADR-0279).
    /// </summary>
    internal const string UploadReaderRewindFailed = "rewind of client reader 'cr-in' failed: 65";

    /// <summary>
    /// Gives an HTTP/3 error code's name as <c>vquic_h3_err_str</c> gives it at
    /// <c>curl-8_21_0</c>: the RFC 9114 name without its <c>H3_</c> prefix, <c>NO_ERROR</c> for
    /// a reserved greasing code (<c>0x21 + 0x1f * N</c>) and <c>unknown</c> for any other.
    /// </summary>
    private static string Http3ErrorName(long errorCode) =>
        errorCode switch
        {
            >= 0x100 and <= 0x110 => Http3ErrorNames[errorCode - 0x100],
            >= 0x21 when (errorCode - 0x21) % 0x1f == 0 => Http3ErrorNames[0],
            _ => "unknown",
        };

    private static readonly string[] Http3ErrorNames =
    [
        "NO_ERROR", "GENERAL_PROTOCOL_ERROR", "INTERNAL_ERROR", "STREAM_CREATION_ERROR", "CLOSED_CRITICAL_STREAM",
        "FRAME_UNEXPECTED", "FRAME_ERROR", "EXCESSIVE_LOAD", "ID_ERROR", "SETTINGS_ERROR", "MISSING_SETTINGS",
        "REQUEST_REJECTED", "REQUEST_CANCELLED", "REQUEST_INCOMPLETE", "MESSAGE_ERROR", "CONNECT_ERROR",
        "VERSION_FALLBACK",
    ];

    /// <summary>
    /// Formats the exit 95 message for an HTTP/3 request stream that ended before the final
    /// response head (<c>curl_ngtcp2.c</c>, ADR-0144 section 7).
    /// </summary>
    /// <param name="streamId">The QUIC stream ID.</param>
    /// <returns>The message.</returns>
    internal static string Http3StreamClosedBeforeHead(long streamId) =>
        string.Create(CultureInfo.InvariantCulture, $"HTTP/3 stream {streamId} was closed cleanly, but before getting all response header fields, treated as error");

    /// <summary>
    /// Formats the exit 56 message for HTTP/3 or QPACK bytes the server sent that break RFC 9114
    /// or RFC 9204, as curl reports nghttp3's refusal: <c>nghttp3_conn_read_stream returned
    /// error: ERR_H3_FRAME_UNEXPECTED</c> (<c>curl_ngtcp2.c</c>, ADR-0172).
    /// </summary>
    /// <param name="errorName">nghttp3's name for the error, such as <c>ERR_H3_FRAME_ERROR</c>.</param>
    /// <returns>The message.</returns>
    internal static string Http3ReadStreamFailed(string errorName) =>
        $"nghttp3_conn_read_stream returned error: {errorName}";

    /// <summary>
    /// Gives an HTTP/2 error code's name as nghttp2's <c>nghttp2_http2_strerror</c> gives it,
    /// <c>unknown</c> for a code RFC 9113 does not list.
    /// </summary>
    private static string Http2ErrorName(Curl.Http2.Http2ErrorCode errorCode) =>
        (uint)errorCode < Http2ErrorNames.Length ? Http2ErrorNames[(uint)errorCode] : "unknown";

    private static readonly string[] Http2ErrorNames =
    [
        "NO_ERROR", "PROTOCOL_ERROR", "INTERNAL_ERROR", "FLOW_CONTROL_ERROR", "SETTINGS_TIMEOUT",
        "STREAM_CLOSED", "FRAME_SIZE_ERROR", "REFUSED_STREAM", "CANCEL", "COMPRESSION_ERROR",
        "CONNECT_ERROR", "ENHANCE_YOUR_CALM", "INADEQUATE_SECURITY", "HTTP_1_1_REQUIRED",
    ];

    /// <summary>
    /// The exit 100 message for one head line, or one header with its continuation lines
    /// folded in, of <see cref="HttpLineReader.MaximumLineLength" /> bytes or more.
    /// </summary>
    internal const string LineTooLarge = "A value or data field grew larger than allowed";

    /// <summary>
    /// The exit 56 message for a chunk size of more than
    /// <see cref="HttpChunkedDecoder.MaximumSizeDigits" /> hexadecimal digits.
    /// </summary>
    internal const string ChunkSizeTooLong = "chunk hex-length longer than 16";

    /// <summary>
    /// The exit 56 message for a byte other than a carriage return or line feed after a
    /// chunk's data, or a carriage return inside a trailer line rather than before its line
    /// feed.
    /// </summary>
    internal const string MalformedChunkedEncoding = "Malformed encoding found in chunked-encoding";

    /// <summary>
    /// The exit 100 message for a trailer line of
    /// <see cref="HttpChunkedDecoder.MaximumTrailerLineLength" /> bytes or more once its
    /// carriage return and line feed are added.
    /// </summary>
    internal const string TrailerTooLarge = "Out of memory in chunked-encoding";

    /// <summary>
    /// The exit 18 message for a peer that closed before a chunked body's last chunk and
    /// trailers were whole.
    /// </summary>
    internal const string ChunkedBodyIncomplete = "transfer closed with outstanding read data remaining";

    /// <summary>
    /// The exit 61 message for a Content-Encoding coding <c>--compressed</c> does not decode.
    /// </summary>
    internal const string UnrecognizedContentEncoding = "Unrecognized content encoding type";

    /// <summary>
    /// The exit 61 message for a <c>gzip</c> body whose first two bytes are neither a gzip
    /// nor a zlib header.
    /// </summary>
    internal const string IncorrectHeaderCheck = "Error while processing content unencoding: incorrect header check";

    /// <summary>
    /// The exit 61 message for a gzip or zlib header that names a compression method other
    /// than deflate.
    /// </summary>
    internal const string UnknownCompressionMethod = "Error while processing content unencoding: unknown compression method";

    /// <summary>
    /// The exit 61 message for any other corrupt encoded body: measured for <c>br</c>, and
    /// the text <c>curl_easy_strerror</c> gives exit 61, used for corrupt <c>gzip</c> and
    /// <c>deflate</c> data too because the BCL does not report zlib's own text (ADR-0031).
    /// </summary>
    internal const string BadContentEncoding = "Unrecognized or bad HTTP Content or Transfer-Encoding";

    /// <summary>
    /// The exit 23 message for a <c>--compressed</c> body with bytes after the end of its
    /// gzip, zlib or Brotli stream (measured, BL-281 Notes).
    /// </summary>
    internal const string ReceivedDataWriteFailed = "Failed writing received data to disk/application";

    /// <summary>
    /// The exit 23 message for a decoded chunked body with bytes after the end of its gzip,
    /// zlib or Brotli stream (measured, BL-365 Notes).
    /// </summary>
    internal const string ChunkedStreamReadFailed = "Failed reading the chunked-encoded stream";

    /// <summary>
    /// The exit 33 message for a <c>-C</c> resume the response does not honour.
    /// </summary>
    internal const string ResumeNotSupported = "HTTP server does not seem to support byte ranges. Cannot resume.";

    /// <summary>
    /// The exit 63 message for a Content-Length over the <c>--max-filesize</c> limit.
    /// </summary>
    internal const string MaximumFileSizeExceeded = "Maximum file size exceeded";

    /// <summary>
    /// The exit 25 message for an HTTP/1.0 request (<c>-0</c>) whose body length is unknown
    /// and that no <c>-H</c> value asks to send chunked (measured, BL-180 Notes).
    /// </summary>
    internal const string ChunkedUploadNeedsHttp11 = "Chunky upload is not supported by HTTP 1.0";

    /// <summary>
    /// The exit 18 message for a <c>-T</c> upload whose <c>-C</c> offset is at or past the
    /// source's length (measured, BL-332 Notes).
    /// </summary>
    internal const string FileAlreadyCompletelyUploaded = "File already completely uploaded";

    /// <summary>
    /// Formats the exit 26 message for a <c>-T</c> upload of an empty source resumed from a
    /// <c>-C</c> offset (measured, BL-332 Notes).
    /// </summary>
    /// <param name="offset">The <c>-C</c> offset.</param>
    /// <returns>The message, such as <c>Unable to resume from offset 3</c>.</returns>
    internal static string UnableToResumeFrom(long offset) =>
        string.Create(CultureInfo.InvariantCulture, $"Unable to resume from offset {offset}");

    /// <summary>
    /// Formats the exit 63 message for a body that grew past the <c>--max-filesize</c> limit,
    /// after as many bytes as the limit allows were written.
    /// </summary>
    /// <param name="maxFileSize">The limit.</param>
    /// <param name="received">The body bytes written.</param>
    /// <returns>
    /// The message, such as <c>Exceeded the maximum allowed file size (10) with 10 bytes</c>.
    /// </returns>
    internal static string FileSizeLimitExceeded(long maxFileSize, long received) =>
        string.Create(CultureInfo.InvariantCulture, $"Exceeded the maximum allowed file size ({maxFileSize}) with {received} bytes");

    /// <summary>
    /// Formats the exit 22 message for a final status of 400 or above under <c>-f</c> or
    /// <c>--fail-with-body</c>.
    /// </summary>
    /// <param name="statusCode">The final status code.</param>
    /// <returns>The message, such as <c>The requested URL returned error: 404</c>.</returns>
    internal static string RequestedUrlReturnedError(int statusCode) =>
        string.Create(CultureInfo.InvariantCulture, $"The requested URL returned error: {statusCode}");

    /// <summary>
    /// Formats the exit 47 message for a <c>417</c> resend that would pass the
    /// <c>--max-redirs</c> limit (measured, BL-396 Notes).
    /// </summary>
    /// <param name="maxRedirects">The limit.</param>
    /// <returns>The message, such as <c>Maximum (50) redirects followed</c>.</returns>
    internal static string MaximumRedirectsFollowed(int maxRedirects) =>
        string.Create(CultureInfo.InvariantCulture, $"Maximum ({maxRedirects}) redirects followed");

    /// <summary>
    /// Formats the exit 56 message for a chunk size line that does not start with a
    /// hexadecimal digit.
    /// </summary>
    /// <param name="value">The offending byte.</param>
    /// <returns>The message, such as <c>chunk hex-length char not a hex digit: 0x7a</c>.</returns>
    internal static string ChunkSizeNotHex(byte value) =>
        string.Create(CultureInfo.InvariantCulture, $"chunk hex-length char not a hex digit: 0x{value:x}");

    /// <summary>
    /// Formats the exit 56 message for a chunk size too large for a signed 64-bit integer.
    /// </summary>
    /// <param name="digits">The chunk size's hexadecimal digits, as received.</param>
    /// <returns>The message, such as <c>invalid chunk size: 'FFFFFFFFFFFFFFFF'</c>.</returns>
    internal static string InvalidChunkSize(string digits) => $"invalid chunk size: '{digits}'";

    /// <summary>
    /// Formats the exit 61 message for a Transfer-Encoding header that lists a coding after
    /// <c>chunked</c>.
    /// </summary>
    /// <param name="coding">The coding listed after <c>chunked</c>, as received.</param>
    /// <returns>The message, such as <c>A Transfer-Encoding (gzip) was listed after chunked</c>.</returns>
    internal static string CodingListedAfterChunked(string coding) =>
        $"A Transfer-Encoding ({coding}) was listed after chunked";

    /// <summary>
    /// Formats the exit 61 message for a transfer coding curl was not asked to decode.
    /// </summary>
    /// <param name="coding">The coding, as received.</param>
    /// <returns>The message, such as <c>Unsolicited Transfer-Encoding (gzip) found</c>.</returns>
    internal static string UnsolicitedTransferCoding(string coding) =>
        $"Unsolicited Transfer-Encoding ({coding}) found";

    /// <summary>
    /// The exit 61 message, under <c>--tr-encoding</c>, for a Transfer-Encoding that lists a
    /// coding after <c>chunked</c>, in the same header or a later one (measured, BL-315 Notes).
    /// </summary>
    internal const string ChunkedNotLast = "Reject response due to 'chunked' not being the last Transfer-Encoding";

    /// <summary>
    /// The exit 61 message, under <c>--tr-encoding</c>, for a Transfer-Encoding that lists more
    /// than five codings across its headers (measured, BL-315 Notes).
    /// </summary>
    internal const string TooManyTransferCodings = "Reject response exceeding limit of 5 transfer encodings";

    /// <summary>
    /// The exit 61 message, under <c>--compressed</c>, for a response whose Content-Encoding
    /// headers list more than five codings (measured, BL-364 Notes).
    /// </summary>
    internal const string TooManyContentCodings = "Reject response exceeding limit of 5 content encodings";

    /// <summary>
    /// Formats the exit 56 message for heads whose combined size passed
    /// <see cref="HttpResponseHeadBuilder.MaximumHeadSize" />.
    /// </summary>
    /// <param name="headSize">The combined size of every head line so far.</param>
    /// <returns>The message, such as <c>Too large response headers: 307201 &gt; 307200</c>.</returns>
    internal static string HeadTooLarge(int headSize) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Too large response headers: {headSize} > {HttpResponseHeadBuilder.MaximumHeadSize}");

    /// <summary>
    /// Chooses the exit 56 message for a failed read: the TLS build's own text when the
    /// connection ended without <c>close_notify</c> (ADR-0221), <see cref="ConnectionReset" />
    /// when the peer reset the connection, and <see cref="ReceiveFailed" /> for anything else.
    /// </summary>
    /// <param name="exception">The failure the read threw.</param>
    /// <returns>The message.</returns>
    internal static string ReceiveFailure(IOException exception) =>
        exception is MissingCloseNotifyException
            ? exception.Message
            : exception.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset }
                ? ConnectionReset
                : ReceiveFailed;

    /// <summary>
    /// The exit 55 message for a write the peer reset (measured, BL-174 Notes).
    /// </summary>
    internal const string SendConnectionReset = "Send failure: Connection was reset";

    /// <summary>
    /// The exit 55 message curl falls back to for any other failed write: the text
    /// <c>curl_easy_strerror</c> gives exit 55.
    /// </summary>
    internal const string SendFailed = "Failed sending data to the peer";

    /// <summary>
    /// Chooses the exit 55 message for a failed write: <see cref="SendConnectionReset" />
    /// when the peer reset the connection, and <see cref="SendFailed" /> for anything else.
    /// </summary>
    /// <param name="exception">The failure the write threw.</param>
    /// <returns>The message.</returns>
    internal static string SendFailure(IOException exception) =>
        exception.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset }
            ? SendConnectionReset
            : SendFailed;

    /// <summary>
    /// Formats the exit 28 message for a connect that the connect timeout or <c>-m</c> ended
    /// (measured, BL-174 Notes).
    /// </summary>
    /// <param name="elapsedMilliseconds">Milliseconds since this request started.</param>
    /// <returns>The message, such as <c>Connection timed out after 1015 milliseconds</c>.</returns>
    internal static string ConnectionTimedOut(long elapsedMilliseconds) =>
        string.Create(CultureInfo.InvariantCulture, $"Connection timed out after {elapsedMilliseconds} milliseconds");

    /// <summary>
    /// Formats the exit 28 message for a transfer <c>-m</c> ended after it connected
    /// (measured, BL-174 Notes).
    /// </summary>
    /// <param name="elapsedMilliseconds">
    /// Milliseconds since the operation started: the first request of a redirect chain.
    /// </param>
    /// <param name="received">The body bytes received so far.</param>
    /// <param name="expected">
    /// The body's Content-Length when the body being read has one, or <see langword="null" />.
    /// </param>
    /// <returns>
    /// The message, such as <c>Operation timed out after 1008 milliseconds with 5 out of 100
    /// bytes received</c>, or <c>... with 5 bytes received</c> when the size is not known.
    /// </returns>
    internal static string OperationTimedOut(long elapsedMilliseconds, long received, long? expected) =>
        expected is { } size
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Operation timed out after {elapsedMilliseconds} milliseconds with {received} out of {size} bytes received")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"Operation timed out after {elapsedMilliseconds} milliseconds with {received} bytes received");

    /// <summary>
    /// Formats the exit 18 message for a peer that closed before the Content-Length body
    /// was whole.
    /// </summary>
    /// <param name="missing">How many body bytes never arrived.</param>
    /// <returns>The message, such as <c>end of response with 7 bytes missing</c>.</returns>
    internal static string BodyBytesMissing(long missing) =>
        string.Create(CultureInfo.InvariantCulture, $"end of response with {missing} bytes missing");

    /// <summary>
    /// Formats the exit 18 message, also its <c>-v</c> line, for a peer that closed among the
    /// final head's headers after a Content-Length curl 8.21.0 acted on (measured, BL-485 Notes).
    /// </summary>
    /// <param name="remaining">How many body bytes the Content-Length still promised.</param>
    /// <returns>The message, such as <c>transfer closed with 5 bytes remaining to read</c>.</returns>
    internal static string TransferClosedWithBytesRemaining(long remaining) =>
        string.Create(CultureInfo.InvariantCulture, $"transfer closed with {remaining} bytes remaining to read");

    /// <summary>
    /// Formats the exit 23 message for an output that stopped accepting body bytes.
    /// </summary>
    /// <param name="passed">The size of the write offered to the output.</param>
    /// <param name="returned">How many bytes of that write the output accepted.</param>
    /// <returns>
    /// The message, such as <c>Failure writing output to destination, passed 16384 returned 0</c>.
    /// </returns>
    internal static string OutputWriteFailed(int passed, int returned) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Failure writing output to destination, passed {passed} returned {returned}");

    /// <summary>
    /// Formats the exit 26 message for a request body stream that failed a read, or ended,
    /// before its known length was sent. curl cannot tell the two apart: a failed read ends
    /// its body reader as the end of the stream does.
    /// </summary>
    /// <param name="read">How many body bytes were read before the stream stopped.</param>
    /// <param name="needed">The body's known length.</param>
    /// <returns>
    /// The message, such as <c>client mime read EOF fail, only 207/100207 of needed bytes read</c>.
    /// </returns>
    internal static string BodyStreamEndedEarly(long read, long needed) =>
        string.Create(CultureInfo.InvariantCulture, $"client mime read EOF fail, only {read}/{needed} of needed bytes read");

    /// <summary>
    /// Formats the exit 26 message for a <c>-T</c> upload source that failed a read, or ended,
    /// before its known length was sent (measured, BL-184 Notes).
    /// </summary>
    /// <param name="read">How many upload bytes were read before the source stopped.</param>
    /// <param name="needed">The upload's known length.</param>
    /// <returns>
    /// The message, such as <c>client read function EOF fail, only 65432/100000 of needed bytes read</c>.
    /// </returns>
    internal static string UploadEndedEarly(long read, long needed) =>
        string.Create(CultureInfo.InvariantCulture, $"client read function EOF fail, only {read}/{needed} of needed bytes read");
}
