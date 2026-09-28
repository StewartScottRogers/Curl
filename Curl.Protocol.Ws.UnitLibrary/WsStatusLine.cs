using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Reads the status code from the first line of the reply to an upgrade request, and refuses
/// a line curl 8.21.0 refuses, with its exit code and message.
/// </summary>
/// <remarks>
/// Measured (BL-580): a line that does not start with <c>HTTP/</c> in any case fails with 1,
/// <c>Received HTTP/0.9 when not allowed</c>; one that starts with it in another case
/// (<c>http/1.1 101</c>) counts as <c>200</c>; <c>HTTP/2</c> and <c>HTTP/3</c> fail with 1,
/// <c>Unsupported HTTP version (2.0) in response</c>; any other major version with 1,
/// <c>Unsupported HTTP version in response</c>; an HTTP/1 line that is not <c>HTTP/1.0</c> or
/// <c>HTTP/1.1</c>, a blank and three digits with 1, <c>Unsupported HTTP/1 subversion in
/// response</c>; and a code below 100 with 1, <c>Unsupported response code in HTTP
/// response</c>.
/// </remarks>
internal static class WsStatusLine
{
    /// <summary>The message for a reply that does not start with <c>HTTP/</c>.</summary>
    internal const string Http09NotAllowed = "Received HTTP/0.9 when not allowed";

    private const string UnsupportedHttpVersion = "Unsupported HTTP version in response";

    private const string UnsupportedHttp1Subversion = "Unsupported HTTP/1 subversion in response";

    private const string UnsupportedResponseCode = "Unsupported response code in HTTP response";

    private const int CodeOffset = 9;

    private const int ReasonOffset = CodeOffset + 3;

    /// <summary>Reads the status code from a status line.</summary>
    /// <param name="line">The status line without its line ending.</param>
    /// <returns>The status code.</returns>
    /// <exception cref="WsTransferException">curl refuses the line.</exception>
    internal static int ParseStatusCode(string line)
    {
        if (!line.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
        {
            throw Unsupported(Http09NotAllowed);
        }

        if (!line.StartsWith("HTTP/", StringComparison.Ordinal))
        {
            return 200;
        }

        return line.Length > 5 ? ParseMajorVersion(line) : throw Unsupported(UnsupportedHttpVersion);
    }

    /// <summary>
    /// Determines whether the bytes received so far can still begin a status line: they match
    /// the start of <c>HTTP/</c> in any case.
    /// </summary>
    /// <param name="received">The bytes received so far.</param>
    /// <returns><see langword="true" /> when the bytes can still begin a status line.</returns>
    internal static bool CanBegin(ReadOnlySpan<byte> received)
    {
        ReadOnlySpan<byte> prefix = "HTTP/"u8;
        int length = Math.Min(received.Length, prefix.Length);
        return System.Text.Ascii.EqualsIgnoreCase(received[..length], prefix[..length]);
    }

    private static int ParseMajorVersion(string line) => line[5] switch
    {
        '1' => ParseHttp1(line),
        '2' or '3' => throw Unsupported(string.Create(CultureInfo.InvariantCulture, $"Unsupported HTTP version ({line[5]}.0) in response")),
        _ => throw Unsupported(UnsupportedHttpVersion),
    };

    private static int ParseHttp1(string line)
    {
        if (!HasHttp1Shape(line))
        {
            throw Unsupported(UnsupportedHttp1Subversion);
        }

        int statusCode = int.Parse(line.AsSpan(CodeOffset, 3), CultureInfo.InvariantCulture);
        return statusCode < 100 ? throw Unsupported(UnsupportedResponseCode) : statusCode;
    }

    private static bool HasHttp1Shape(string line) =>
        line.Length >= ReasonOffset
        && IsHttp1Version(line.AsSpan(5, 3))
        && IsBlank(line[8])
        && !line.AsSpan(CodeOffset, 3).ContainsAnyExceptInRange('0', '9');

    private static bool IsHttp1Version(ReadOnlySpan<char> version) => version is "1.0" or "1.1";

    private static bool IsBlank(char character) => character is ' ' or '\t';

    private static WsTransferException Unsupported(string message) =>
        new(CurlExitCode.UnsupportedProtocol, message);
}
