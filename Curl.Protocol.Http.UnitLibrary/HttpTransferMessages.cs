using System.Globalization;
using System.Net.Sockets;

namespace Curl.Protocol.Http;

/// <summary>
/// Every failure message reading an HTTP/1.x response head or body reports, as curl 8.21.0
/// prints it. Each was measured against a loopback server (BL-169, BL-170) except
/// <see cref="ReceiveFailed" />, which is the text <c>curl_easy_strerror</c> gives exit 56.
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
}
