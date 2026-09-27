namespace Curl.Protocol.Abstractions;

/// <summary>
/// An HTTP request body already in memory, such as every <c>-d</c> form, which the
/// command-line layer reads in full at parse time. It is sent with
/// <c>Content-Length: Content.Length</c> (ADR-0014).
/// </summary>
/// <param name="Content">The body bytes, sent verbatim.</param>
/// <param name="ContentType">The value of the <c>Content-Type</c> header; never null.</param>
/// <exception cref="ArgumentNullException"><paramref name="ContentType" /> is <see langword="null" />.</exception>
public sealed record BytesBody(ReadOnlyMemory<byte> Content, string ContentType)
    : HttpRequestBody(ContentType);
