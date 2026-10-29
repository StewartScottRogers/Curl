using System.Buffers;
using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using Curl.Http2;

namespace Curl.Protocol.Http;

/// <summary>
/// Writes an HTTP/2 response's header blocks as the bytes curl 8.21.0's HTTP/2 layer hands its
/// HTTP/1 header parser and its header output: a head is <c>HTTP/2 200 \r\n</c> (the status
/// with a blank after it), one <c>name: value\r\n</c> line per header as received, and the
/// empty line; trailers are the header lines alone, with no empty line after them (measured
/// with curl.se's nghttp2 build, BL-658 Notes).
/// </summary>
internal static class Http2ResponseHead
{
    private static readonly SearchValues<char> TokenCharacters =
        SearchValues.Create("!#$%&'*+-.^_`|~0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");

    private static readonly FrozenSet<string> ConnectionSpecificNames =
        FrozenSet.Create(StringComparer.Ordinal, "connection", "keep-alive", "proxy-connection", "transfer-encoding", "upgrade");

    /// <summary>
    /// Finds the <c>:status</c> of a response header block: three digits from 100 to 999, or
    /// <see langword="null" /> when it has none or another value, which makes the response
    /// malformed (RFC 9113 section 8.3.2).
    /// </summary>
    /// <param name="fields">The decoded header block.</param>
    /// <returns>The status code, or <see langword="null" />.</returns>
    internal static int? StatusOf(IReadOnlyList<HeaderField> fields)
    {
        string? status = fields.FirstOrDefault(field => field.Name == ":status").Value;
        return status is { Length: 3 } && status[0] != '0' && !status.AsSpan().ContainsAnyExceptInRange('0', '9')
            ? int.Parse(status, CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>
    /// Finds the first field of a response header block that nghttp2 1.68 rejects as an invalid
    /// header field, which curl fails the transfer for (RFC 9113 sections 8.2 and 8.3, measured,
    /// BL-3664 Notes), or -1 when there is none. A pseudo-header is invalid in trailers, after a
    /// regular field or another pseudo-header, when it is not <c>:status</c>, or when it is
    /// <c>:status 101</c>; a regular field is invalid when its name has an upper-case letter,
    /// when it is connection-specific (<c>te</c> other than <c>trailers</c>), or when it is a
    /// second <c>content-length</c> or one that is not a decimal number. A field nghttp2 ignores
    /// (<see cref="IsIgnored" />) is skipped; a <c>:status</c> value that is not three digits is
    /// left to <see cref="StatusOf" />.
    /// </summary>
    /// <param name="fields">The decoded header block.</param>
    /// <param name="isTrailer">Whether the block came after the final head.</param>
    /// <returns>The index of the first invalid field, or -1.</returns>
    internal static int IndexOfInvalidField(IReadOnlyList<HeaderField> fields, bool isTrailer)
    {
        bool isPseudoHeaderAllowed = !isTrailer;
        bool isContentLengthSeen = false;
        for (int index = 0; index < fields.Count; index++)
        {
            HeaderField field = fields[index];
            if (IsIgnoredRegularField(field))
            {
                continue;
            }

            if (!IsValidField(field, isPseudoHeaderAllowed, isContentLengthSeen))
            {
                return index;
            }

            isPseudoHeaderAllowed = false;
            isContentLengthSeen |= field.Name == "content-length";
        }

        return -1;
    }

    private static bool IsIgnoredRegularField(HeaderField field) => !field.Name.StartsWith(':') && IsIgnored(field);

    private static bool IsValidField(HeaderField field, bool isPseudoHeaderAllowed, bool isContentLengthSeen) =>
        field.Name.StartsWith(':') ? isPseudoHeaderAllowed && IsValidStatus(field) : IsValidRegularField(field, isContentLengthSeen);

    private static bool IsValidStatus(HeaderField field) => field.Name == ":status" && field.Value != "101";

    private static bool IsValidRegularField(HeaderField field, bool isContentLengthSeen) =>
        !field.Name.AsSpan().ContainsAnyInRange('A', 'Z')
        && !IsConnectionSpecific(field)
        && (field.Name != "content-length" || IsValidContentLength(field.Value, isContentLengthSeen));

    /// <summary>
    /// Tells whether <paramref name="field" /> is a connection-specific field nghttp2 rejects in
    /// a response (RFC 9113 section 8.2.2): <c>connection</c>, <c>keep-alive</c>,
    /// <c>proxy-connection</c>, <c>transfer-encoding</c>, <c>upgrade</c>, or <c>te</c> with a
    /// value other than <c>trailers</c> (CGR-136, measured, BL-3665 Notes).
    /// </summary>
    private static bool IsConnectionSpecific(HeaderField field) =>
        ConnectionSpecificNames.Contains(field.Name)
        || (field.Name == "te" && !field.Value.Equals("trailers", StringComparison.OrdinalIgnoreCase));

    private static bool IsValidContentLength(string value, bool isContentLengthSeen) =>
        !isContentLengthSeen && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);

    /// <summary>
    /// Gives the head bytes for a header block with <paramref name="statusCode" />: the status
    /// line, a line for each header that is not a pseudo-header, and the empty line. curl's
    /// HTTP/3 layer writes an HTTP/3 field section the same way, <c>HTTP/3 200 \r\n</c> (ADR-0144).
    /// </summary>
    /// <param name="versionName">The version the status line names: <c>HTTP/2</c> or <c>HTTP/3</c>.</param>
    /// <param name="statusCode">The block's <c>:status</c>.</param>
    /// <param name="fields">The decoded header block.</param>
    /// <returns>The head, as Latin-1 bytes.</returns>
    internal static byte[] Format(string versionName, int statusCode, IReadOnlyList<HeaderField> fields) =>
        Encoding.Latin1.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{versionName} {statusCode:000} \r\n{HeaderLines(fields)}\r\n"));

    /// <summary>
    /// Gives the trailer bytes for a trailing header block: a line for each header that is not
    /// a pseudo-header, and nothing after them.
    /// </summary>
    /// <param name="fields">The decoded trailing header block.</param>
    /// <returns>The trailer lines, as Latin-1 bytes.</returns>
    internal static byte[] FormatTrailers(IReadOnlyList<HeaderField> fields) =>
        Encoding.Latin1.GetBytes(HeaderLines(fields));

    /// <summary>
    /// Joins a line for each header that is not a pseudo-header, leaving out any field whose
    /// value holds a CR, LF or NUL, or whose name is empty or holds a character that is not a
    /// token character (a space or a colon, say): nghttp2 treats such a field as malformed (RFC
    /// 9113 section 8.2.1) and curl ignores it without a message, so a server cannot inject
    /// header lines (CGR-011, CGR-137).
    /// </summary>
    private static string HeaderLines(IReadOnlyList<HeaderField> fields) =>
        string.Concat(fields.Where(field => !field.Name.StartsWith(':') && !IsIgnored(field)).Select(field => $"{field.Name}: {field.Value}\r\n"));

    /// <summary>
    /// Tells whether nghttp2 ignores a regular field without a message: its value holds a CR, LF
    /// or NUL, or its name is not a token (<see cref="HeaderLines" />).
    /// </summary>
    internal static bool IsIgnored(HeaderField field) =>
        !IsTokenName(field.Name) || field.Value.AsSpan().ContainsAny("\r\n\0");

    /// <summary>
    /// Tells whether <paramref name="name" /> is a field name nghttp2 hands on: one or more
    /// token characters (RFC 9110 section 5.6.2).
    /// </summary>
    private static bool IsTokenName(string name) =>
        name.Length > 0 && !name.AsSpan().ContainsAnyExcept(TokenCharacters);
}
