using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// The status line of one HTTP/1.x response, parsed as curl 8.21.0 parses it, or of an
/// HTTP/2 response as <see cref="Http2ResponseHead" /> writes it.
/// </summary>
/// <param name="version">The protocol version: <see cref="HttpVersion.Version10" />, <see cref="HttpVersion.Version11" /> or <see cref="HttpVersion.Version20" />, or 0.9 for <see cref="Http09" />.</param>
/// <param name="statusCode">The three-digit status code, from 100 to 999, or 0 for <see cref="Http09" />.</param>
/// <param name="reasonPhrase">The text after the status code, without leading blanks; empty when there is none.</param>
internal sealed class HttpStatusLine(Version version, int statusCode, string reasonPhrase)
{
    private const int CodeOffset = 9;

    private const int ReasonOffset = CodeOffset + 3;

    /// <summary>
    /// Gets the protocol version: <see cref="HttpVersion.Version10" />, <see cref="HttpVersion.Version11" /> or <see cref="HttpVersion.Version20" />, or 0.9 for <see cref="Http09" />.
    /// </summary>
    internal Version Version { get; } = version;

    /// <summary>
    /// Gets the three-digit status code, from 100 to 999, or 0 for <see cref="Http09" />.
    /// </summary>
    internal int StatusCode { get; } = statusCode;

    /// <summary>
    /// Gets the text after the status code, without leading blanks; empty when there is none.
    /// </summary>
    internal string ReasonPhrase { get; } = reasonPhrase;

    /// <summary>
    /// Gets a value indicating whether this is a 1xx informational response, which curl
    /// reads past to the final response.
    /// </summary>
    internal bool IsInformational => StatusCode < 200;

    /// <summary>
    /// Parses a status line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A line beginning <c>HTTP/1</c> must continue <c>.0</c> or <c>.1</c>, a blank and
    /// three digits. A line beginning <c>HTTP/2</c> or <c>HTTP/3</c>, or <c>HTTP/</c> in
    /// another letter case, is taken as <c>HTTP/1.0 200</c>, as curl 8.21.0 does over an
    /// HTTP/1.x connection.
    /// </para>
    /// </remarks>
    /// <param name="line">The line, without its terminator.</param>
    /// <returns>The status line.</returns>
    /// <exception cref="HttpTransferException">
    /// The line does not begin <c>HTTP/</c> (exit 1, <c>Received HTTP/0.9 when not
    /// allowed</c>), names an unsupported version (exit 1), or carries a status code below
    /// 100 (exit 1, <c>Unsupported response code in HTTP response</c>), or carries a status
    /// code of four or more digits (exit 8, <c>Invalid status line</c>).
    /// </exception>
    internal static HttpStatusLine Parse(string line)
    {
        if (!line.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
        {
            throw Unsupported(HttpTransferMessages.Http09NotAllowed);
        }

        if (!line.StartsWith("HTTP/", StringComparison.Ordinal))
        {
            return AssumedOk();
        }

        return line.Length > 5 ? ParseMajorVersion(line) : throw Unsupported(HttpTransferMessages.UnsupportedHttpVersion);
    }

    /// <summary>
    /// Parses the status line <see cref="Http2ResponseHead.Format" /> writes for an HTTP/2 or
    /// HTTP/3 response, <c>HTTP/2 200 </c> or <c>HTTP/3 200 </c>: version 2.0 or 3.0, the
    /// status code, and no reason phrase.
    /// </summary>
    /// <param name="line">The line, without its terminator.</param>
    /// <returns>The status line.</returns>
    internal static HttpStatusLine ParseHttp2OrHttp3(string line) =>
        new(
            line[5] == '3' ? HttpVersion.Version30 : HttpVersion.Version20,
            int.Parse(line.AsSpan(7, 3), System.Globalization.CultureInfo.InvariantCulture),
            string.Empty);

    /// <summary>
    /// Tells whether bytes received so far could still begin a status line: they must match
    /// <c>HTTP/</c>, in any letter case, as far as they go.
    /// </summary>
    /// <param name="received">The first bytes of the line.</param>
    /// <exception cref="HttpTransferException">
    /// They cannot (exit 1, <c>Received HTTP/0.9 when not allowed</c>).
    /// </exception>
    internal static void RejectHttp09(ReadOnlySpan<byte> received)
    {
        if (!CanBeginHttp(received))
        {
            throw Unsupported(HttpTransferMessages.Http09NotAllowed);
        }
    }

    /// <summary>
    /// Tells whether bytes received so far could still begin a status line: they match
    /// <c>HTTP/</c>, in any letter case, as far as they go.
    /// </summary>
    /// <param name="received">The first bytes of the response.</param>
    /// <returns><see langword="false" /> when they cannot, so the response is HTTP/0.9.</returns>
    internal static bool CanBeginHttp(ReadOnlySpan<byte> received)
    {
        ReadOnlySpan<byte> prefix = "HTTP/"u8;
        int length = Math.Min(received.Length, prefix.Length);
        return System.Text.Ascii.EqualsIgnoreCase(received[..length], prefix[..length]);
    }

    /// <summary>
    /// Gives the status line of an HTTP/0.9 response that <c>--http0.9</c> accepts, which has
    /// none: version 0.9, status code 0 and no reason phrase, so <c>%{http_code}</c> writes
    /// <c>000</c> and <c>%{http_version}</c> writes <c>0</c>, as curl 8.21.0 does (measured,
    /// BL-1275 Notes).
    /// </summary>
    /// <returns>The HTTP/0.9 status line.</returns>
    internal static HttpStatusLine Http09() => new(new Version(0, 9), 0, string.Empty);

    private static HttpStatusLine ParseMajorVersion(string line) => line[5] switch
    {
        '1' => ParseHttp1(line),
        '2' or '3' => AssumedOk(),
        _ => throw Unsupported(HttpTransferMessages.UnsupportedHttpVersion),
    };

    private static HttpStatusLine ParseHttp1(string line)
    {
        if (!HasHttp1Shape(line))
        {
            throw Unsupported(HttpTransferMessages.UnsupportedHttp1Subversion);
        }

        int statusCode = int.Parse(line.AsSpan(CodeOffset, 3), System.Globalization.CultureInfo.InvariantCulture);
        if (statusCode < 100)
        {
            throw Unsupported(HttpTransferMessages.UnsupportedResponseCode);
        }

        // Checked after the code below 100, as curl's test 1432 (a 100-digit code starting 012) exits 1.
        if (line.Length > ReasonOffset && char.IsAsciiDigit(line[ReasonOffset]))
        {
            throw new HttpTransferException(CurlExitCode.WeirdServerReply, HttpTransferMessages.InvalidStatusLine);
        }

        Version version = line[7] == '0' ? HttpVersion.Version10 : HttpVersion.Version11;
        return new HttpStatusLine(version, statusCode, line[ReasonOffset..].TrimStart(' ', '\t'));
    }

    private static bool HasHttp1Shape(string line) =>
        line.Length >= ReasonOffset
        && line.AsSpan(5, 3) is "1.0" or "1.1"
        && HttpLine.IsBlank(line[8])
        && IsThreeDigits(line.AsSpan(CodeOffset, 3));

    private static bool IsThreeDigits(ReadOnlySpan<char> code) => !code.ContainsAnyExceptInRange('0', '9');

    private static HttpStatusLine AssumedOk() => new(HttpVersion.Version10, 200, string.Empty);

    private static HttpTransferException Unsupported(string message) =>
        new(CurlExitCode.UnsupportedProtocol, message);
}
