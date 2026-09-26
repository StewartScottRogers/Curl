namespace Curl.Protocol.Abstractions;

/// <summary>
/// An HTTP request body produced while sending, such as a multipart body with a file
/// part (ADR-0014).
/// </summary>
/// <param name="Content">
/// The stream the body is read from. The handler reads it but does not own it; whoever
/// built the body disposes it.
/// </param>
/// <param name="Length">
/// The body's size in bytes, sent as <c>Content-Length</c>, or <see langword="null" />
/// when the size is unknown and the body is sent with <c>Transfer-Encoding: chunked</c>,
/// as curl does.
/// </param>
/// <param name="ContentType">The value of the <c>Content-Type</c> header; never null.</param>
/// <exception cref="ArgumentNullException"><paramref name="ContentType" /> is <see langword="null" />.</exception>
public sealed record StreamBody(Stream Content, long? Length, string ContentType)
    : HttpRequestBody(ContentType);
