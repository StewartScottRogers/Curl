namespace Curl.Protocol.Http;

/// <summary>
/// The final, non-1xx head of an HTTP/1.x response: its status line, its headers, the head
/// bytes as curl writes them for <c>-D</c>, and the body bytes read along with it.
/// </summary>
/// <param name="statusLine">The final response's status line.</param>
/// <param name="headers">The final response's headers, in the order received.</param>
/// <param name="headBytes">Every head read, 1xx heads included.</param>
/// <param name="bodyPrefix">The bytes that arrived after the final head in the same reads.</param>
internal sealed class HttpResponseHead(
    HttpStatusLine statusLine,
    IReadOnlyList<HttpResponseHeader> headers,
    ReadOnlyMemory<byte> headBytes,
    ReadOnlyMemory<byte> bodyPrefix)
{
    /// <summary>
    /// Gets the final response's status line.
    /// </summary>
    internal HttpStatusLine StatusLine { get; } = statusLine;

    /// <summary>
    /// Gets the final response's headers, in the order received.
    /// </summary>
    internal IReadOnlyList<HttpResponseHeader> Headers { get; } = headers;

    /// <summary>
    /// Gets every head read, 1xx heads included, each with its continuation lines folded, as
    /// curl 8.21.0 writes them for <c>-D</c>. Its length is curl's <c>%{size_header}</c>.
    /// </summary>
    internal ReadOnlyMemory<byte> HeadBytes { get; } = headBytes;

    /// <summary>
    /// Gets the bytes that arrived after the final head in the same reads: the start of the
    /// body, and empty when the peer closed inside the head.
    /// </summary>
    internal ReadOnlyMemory<byte> BodyPrefix { get; } = bodyPrefix;

    /// <summary>
    /// Cuts the head short before one of its headers: the head as curl 8.21.0 has read and
    /// written it when it refuses that header (measured, BL-412 Notes), with the headers and
    /// head lines before it, no line ending the head, and no body.
    /// </summary>
    /// <param name="headerIndex">The index in <see cref="Headers" /> of the refused header.</param>
    /// <returns>The head before that header.</returns>
    internal HttpResponseHead Before(int headerIndex) =>
        new(StatusLine, [.. Headers.Take(headerIndex)], HeadBytes[..Headers[headerIndex].LineStart], ReadOnlyMemory<byte>.Empty);
}
