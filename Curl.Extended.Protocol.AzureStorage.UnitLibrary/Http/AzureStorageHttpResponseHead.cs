using System.Globalization;

namespace Curl.Extended.Protocol.AzureStorage.Http;

/// <summary>Parses a response's status line (RFC 9112 §4) and header fields (RFC 9112 §5), and decides its body's framing (RFC 9112 §6.3).</summary>
internal static class AzureStorageHttpResponseHead
{
    /// <summary>Parses <c>HTTP/1.x NNN reason</c>.</summary>
    /// <param name="line">The status line.</param>
    /// <returns>The status code, the reason phrase and whether the version is HTTP/1.1 or later.</returns>
    public static (int StatusCode, string ReasonPhrase, bool IsHttp11) ParseStatusLine(string line)
    {
        if (!StartsWithHttp1Version(line) || !int.TryParse(line.AsSpan(9, 3), NumberStyles.None, CultureInfo.InvariantCulture, out var statusCode)
            || (line.Length > 12 && line[12] != ' '))
        {
            throw new AzureStorageHttpException(AzureStorageHttpFailure.MalformedStatusLine, $"The status line \"{line}\" is not HTTP/1.x NNN reason.");
        }

        return (statusCode, line.Length > 12 ? line[13..] : string.Empty, line[7] != '0');
    }

    private static bool StartsWithHttp1Version(string line) =>
        line.Length >= 12 && line.StartsWith("HTTP/1.", StringComparison.Ordinal) && char.IsAsciiDigit(line[7]) && line[8] == ' ';

    /// <summary>Parses <c>name: value</c>, trimming the value's surrounding spaces and tabs.</summary>
    /// <param name="line">The field line.</param>
    /// <returns>The field.</returns>
    public static KeyValuePair<string, string> ParseField(string line)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0 || line.AsSpan(0, colon).IndexOfAny(' ', '\t') >= 0)
        {
            throw new AzureStorageHttpException(AzureStorageHttpFailure.MalformedHeaderField, $"The header field line \"{line}\" is not name: value.");
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
    /// <param name="hasNoBody">Whether the response cannot have a body: it answers <c>HEAD</c>, or is 1xx, 204 or 304 (rule 1).</param>
    /// <returns>
    /// <see langword="null"/> for a chunked body (rule 4), -1 for a body delimited by the close (rules 4 and 8), or the body's length
    /// (rules 1 and 6).
    /// </returns>
    public static long? FindBodyLength(IReadOnlyList<KeyValuePair<string, string>> headers, bool hasNoBody)
    {
        if (hasNoBody)
        {
            return 0;
        }

        var transferEncoding = FindHeader(headers, "Transfer-Encoding");
        if (transferEncoding is not null)
        {
            return transferEncoding.Split(',').Last().Trim().Equals("chunked", StringComparison.OrdinalIgnoreCase) ? null : -1;
        }

        var contentLength = FindHeader(headers, "Content-Length");
        if (contentLength is null)
        {
            return -1;
        }

        return long.TryParse(contentLength, NumberStyles.None, CultureInfo.InvariantCulture, out var length)
            ? length
            : throw new AzureStorageHttpException(AzureStorageHttpFailure.InvalidContentLength, $"The Content-Length \"{contentLength}\" is not a decimal length.");
    }

    /// <summary>Whether the response leaves the connection open (RFC 9112 §9.3).</summary>
    /// <param name="headers">The response's fields.</param>
    /// <param name="isHttp11">Whether the response is HTTP/1.1.</param>
    /// <param name="bodyLength">The framing <see cref="FindBodyLength"/> decided.</param>
    /// <returns><see langword="true"/> when another request can follow on the connection.</returns>
    public static bool ConnectionPersists(IReadOnlyList<KeyValuePair<string, string>> headers, bool isHttp11, long? bodyLength) =>
        isHttp11 && bodyLength != -1
        && !(FindHeader(headers, "Connection") ?? string.Empty).Split(',').Any(option => option.Trim().Equals("close", StringComparison.OrdinalIgnoreCase));
}
