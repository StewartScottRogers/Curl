using System.Globalization;

namespace Curl.Extension.Protocol.OciRegistry.Http;

/// <summary>Parses a response's status line (RFC 9112 §4) and header fields (RFC 9112 §5), and decides its body's framing (RFC 9112 §6.3).</summary>
internal static class OciHttpResponseHead
{
    /// <summary>Parses <c>HTTP/1.x NNN reason</c>.</summary>
    /// <param name="line">The status line.</param>
    /// <returns>The status code and the reason phrase.</returns>
    public static (int StatusCode, string ReasonPhrase) ParseStatusLine(string line)
    {
        if (!StartsWithHttp1Version(line) || !int.TryParse(line.AsSpan(9, 3), NumberStyles.None, CultureInfo.InvariantCulture, out var statusCode)
            || (line.Length > 12 && line[12] != ' '))
        {
            throw OciHttpException.Malformed(OciHttpFailure.MalformedStatusLine, $"The status line \"{line}\" is not HTTP/1.x NNN reason.");
        }

        return (statusCode, line.Length > 12 ? line[13..] : string.Empty);
    }

    /// <summary>Parses <c>name: value</c>, trimming the value's surrounding spaces and tabs.</summary>
    /// <param name="line">The field line.</param>
    /// <returns>The field.</returns>
    public static KeyValuePair<string, string> ParseField(string line)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0 || line.AsSpan(0, colon).IndexOfAny(' ', '\t') >= 0)
        {
            throw OciHttpException.Malformed(OciHttpFailure.MalformedHeaderField, $"The header field line \"{line}\" is not name: value.");
        }

        return new(line[..colon], line[(colon + 1)..].Trim(' ', '\t'));
    }

    /// <summary>Finds the first field named <paramref name="name"/>, compared case-insensitively.</summary>
    /// <param name="headers">The fields.</param>
    /// <param name="name">The field name.</param>
    /// <returns>Its value, or <see langword="null"/>.</returns>
    public static string? FindHeader(IReadOnlyList<KeyValuePair<string, string>> headers, string name)
    {
        foreach (var header in headers)
        {
            if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return header.Value;
            }
        }

        return null;
    }

    /// <summary>Decides how the body is framed (RFC 9112 §6.3).</summary>
    /// <param name="headers">The response's fields.</param>
    /// <param name="statusCode">The final status code; a 204 or 304 has no body (rule 1).</param>
    /// <returns>
    /// <see langword="null"/> for a chunked body (rule 4), -1 for a body delimited by the close (rules 4 and 8), or the body's length
    /// (rules 1 and 6).
    /// </returns>
    public static long? FindBodyLength(IReadOnlyList<KeyValuePair<string, string>> headers, int statusCode)
    {
        if (statusCode is 204 or 304)
        {
            return 0;
        }

        var transferEncoding = FindHeader(headers, "Transfer-Encoding");
        return transferEncoding is not null ? FramingOfTransferEncoding(transferEncoding) : FramingOfContentLength(FindHeader(headers, "Content-Length"));
    }

    // Rule 4: chunked last means chunked, any other coding last means the close.
    private static long? FramingOfTransferEncoding(string transferEncoding) =>
        transferEncoding.Split(',').Last().Trim().Equals("chunked", StringComparison.OrdinalIgnoreCase) ? null : -1;

    // Rules 6 and 8: a Content-Length gives the length; without one the close ends the body.
    private static long FramingOfContentLength(string? contentLength) =>
        contentLength is null ? -1 : ParseContentLength(contentLength);

    private static long ParseContentLength(string contentLength) =>
        long.TryParse(contentLength, NumberStyles.None, CultureInfo.InvariantCulture, out var length)
            ? length
            : throw OciHttpException.Malformed(OciHttpFailure.InvalidContentLength, $"The Content-Length \"{contentLength}\" is not a decimal length.");

    private static bool StartsWithHttp1Version(string line) =>
        line.Length >= 12 && line.StartsWith("HTTP/1.", StringComparison.Ordinal) && char.IsAsciiDigit(line[7]) && line[8] == ' ';
}
