using System.Globalization;

namespace Curl.Protocol.Http;

/// <summary>
/// Every failure message reading an HTTP/1.x response head reports, as curl 8.21.0 prints
/// it. Each was measured against a loopback server (BL-169) except
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
}
