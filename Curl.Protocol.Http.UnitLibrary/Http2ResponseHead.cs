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

    private static string HeaderLines(IReadOnlyList<HeaderField> fields) =>
        string.Concat(fields.Where(field => !field.Name.StartsWith(':')).Select(field => $"{field.Name}: {field.Value}\r\n"));
}
