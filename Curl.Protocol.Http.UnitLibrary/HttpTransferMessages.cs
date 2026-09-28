using System.Globalization;
using System.Net.Sockets;

namespace Curl.Protocol.Http;

/// <summary>
/// Every failure message sending an HTTP/1.x request body or reading a response head or
/// body reports, as curl 8.21.0 prints it. Each was measured against a loopback server
/// (BL-169, BL-170, BL-171, BL-174, BL-175, BL-176, BL-178, BL-180) except <see cref="ReceiveFailed" /> and
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
    /// Chooses the exit 56 message for a failed read: <see cref="ConnectionReset" /> when the
    /// peer reset the connection, and <see cref="ReceiveFailed" /> for anything else.
    /// </summary>
    /// <param name="exception">The failure the read threw.</param>
    /// <returns>The message.</returns>
    internal static string ReceiveFailure(IOException exception) =>
        exception.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset }
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
