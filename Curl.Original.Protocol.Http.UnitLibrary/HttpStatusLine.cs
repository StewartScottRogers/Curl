using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

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
    /// 100 (exit 1, <c>Unsupported response code in HTTP response</c>, the line parsed as its
    /// <see cref="HttpTransferException.StatusLineBeforeFailure" />). Digits after the first three
    /// are not part of the code.
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
    /// Tells whether a status line received over HTTP/1.x names HTTP/2 or HTTP/3 in the shape
    /// curl's OpenSSL build parses, <c>HTTP/2 </c> or <c>HTTP/3 </c>, a three-digit code and a
    /// blank, rather than taking it as an assumed <c>HTTP/1.0 200</c> (measured, BL-2715 Notes);
    /// <c>HTTP/2 200</c> with nothing after the code is taken as that (measured, BL-3646 Notes).
    /// </summary>
    /// <param name="line">The line, without its terminator.</param>
    /// <returns><see langword="true" /> when it does.</returns>
    internal static bool NamesHttp2OrHttp3(string line) =>
        line.Length > 10
        && (line.StartsWith("HTTP/2 ", StringComparison.Ordinal) || line.StartsWith("HTTP/3 ", StringComparison.Ordinal))
        && IsThreeDigits(line.AsSpan(7, 3))
        && HttpLine.IsBlank(line[10]);

    /// <summary>
    /// Gives the failure curl reports for a status line over HTTP/1.x that
    /// <see cref="NamesHttp2OrHttp3" />. A build with HTTP/2 (OpenSSL) exits 8, <c>Version mismatch
    /// (from HTTP/1 to HTTP/2)</c>, for HTTP/2 (upstream test1704); a build without it (the Schannel
    /// build on Windows, BL-3730 Notes) exits 1, <c>Unsupported HTTP version (2.0) in response</c>.
    /// HTTP/3 is always exit 1, <c>Unsupported HTTP version (3.0) in response</c> (measured,
    /// BL-2715 Notes).
    /// </summary>
    /// <param name="line">The line, without its terminator.</param>
    /// <param name="withoutHttp2">Whether the curl being matched is built without HTTP/2.</param>
    /// <returns>The failure to throw.</returns>
    internal static HttpTransferException RefusalOverHttp1(string line, bool withoutHttp2) =>
        line[5] == '2' && !withoutHttp2
            ? new(CurlExitCode.WeirdServerReply, HttpTransferMessages.VersionMismatchFromHttp1ToHttp2)
            : Unsupported(line[5] == '2' ? HttpTransferMessages.UnsupportedHttp2Version : HttpTransferMessages.UnsupportedHttp3Version);

    /// <summary>
    /// Rejects bytes received so far that cannot begin a status line: they must match
    /// <c>HTTP/</c> or one of the <c>CURLOPT_HTTP200ALIASES</c>, in any letter case, as far as they go.
    /// </summary>
    /// <param name="received">The first bytes of the line.</param>
    /// <param name="http200Aliases">The <c>CURLOPT_HTTP200ALIASES</c> status lines.</param>
    /// <exception cref="HttpTransferException">
    /// They cannot (exit 1, <c>Received HTTP/0.9 when not allowed</c>).
    /// </exception>
    internal static void RejectHttp09(ReadOnlySpan<byte> received, IReadOnlyList<string> http200Aliases)
    {
        if (!CanBeginStatusLine(received, http200Aliases))
        {
            throw Unsupported(HttpTransferMessages.Http09NotAllowed);
        }
    }

    /// <summary>
    /// Tells whether bytes received so far could still begin a status line: <see cref="CanBeginHttp" />,
    /// or they match one of the <c>CURLOPT_HTTP200ALIASES</c>, in any letter case, as far as both go,
    /// as curl 8.22.0's <c>checkprefixmax</c> compares them.
    /// </summary>
    /// <param name="received">The first bytes of the response.</param>
    /// <param name="http200Aliases">The <c>CURLOPT_HTTP200ALIASES</c> status lines.</param>
    /// <returns><see langword="false" /> when they cannot, so the response is HTTP/0.9.</returns>
    internal static bool CanBeginStatusLine(ReadOnlySpan<byte> received, IReadOnlyList<string> http200Aliases)
    {
        if (CanBeginHttp(received))
        {
            return true;
        }

        foreach (string alias in http200Aliases)
        {
            int length = Math.Min(received.Length, alias.Length);
            if (System.Text.Ascii.EqualsIgnoreCase(received[..length], alias.AsSpan(0, length)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tells whether a status line begins with one of the <c>CURLOPT_HTTP200ALIASES</c>, in any
    /// letter case, comparing as far as the shorter of the two goes, as curl 8.22.0's
    /// <c>checkprefixmax</c> does.
    /// </summary>
    /// <param name="line">The line, without its terminator.</param>
    /// <param name="http200Aliases">The <c>CURLOPT_HTTP200ALIASES</c> status lines.</param>
    /// <returns><see langword="true" /> when it does.</returns>
    internal static bool MatchesAlias(string line, IReadOnlyList<string> http200Aliases)
    {
        foreach (string alias in http200Aliases)
        {
            int length = Math.Min(line.Length, alias.Length);
            if (line.AsSpan(0, length).Equals(alias.AsSpan(0, length), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gives the status line curl 8.22.0 takes a <c>CURLOPT_HTTP200ALIASES</c> line as:
    /// <c>HTTP/1.0 200</c> with no reason phrase.
    /// </summary>
    /// <returns>The status line.</returns>
    internal static HttpStatusLine Http200Alias() => new(HttpVersion.Version10, 200, string.Empty);

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

        // Only the first three digits are the code: curl 8.22.0 reads "2000" as 200 and "1000" as 100 (measured, BL-3646 Notes).
        int statusCode = int.Parse(line.AsSpan(CodeOffset, 3), System.Globalization.CultureInfo.InvariantCulture);
        Version version = line[7] == '0' ? HttpVersion.Version10 : HttpVersion.Version11;
        HttpStatusLine statusLine = new(version, statusCode, line[ReasonOffset..].TrimStart(' ', '\t'));
        return statusCode < 100
            ? throw new HttpTransferException(CurlExitCode.UnsupportedProtocol, HttpTransferMessages.UnsupportedResponseCode) { StatusLineBeforeFailure = statusLine }
            : statusLine;
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
