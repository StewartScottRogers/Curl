namespace Curl.Protocol.Abstractions;

/// <summary>
/// An HTTP request body from the <c>-d</c> or <c>-F</c> families, already encoded by the
/// command-line layer, with the <c>Content-Type</c> it is sent with (ADR-0014).
/// </summary>
/// <remarks>
/// curl sends such a body as POST. A <c>-T</c>/<c>--upload-file</c> body is not one of
/// these; it stays on <see cref="ITransferContext.Upload" /> and is sent as PUT.
/// </remarks>
public abstract record HttpRequestBody
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRequestBody" /> class.
    /// </summary>
    /// <param name="contentType">The value of the <c>Content-Type</c> header; never null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="contentType" /> is <see langword="null" />.</exception>
    protected HttpRequestBody(string contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType, nameof(ContentType));

        ContentType = contentType;
    }

    /// <summary>
    /// Gets the value of the <c>Content-Type</c> header the body is sent with, for example
    /// <c>application/x-www-form-urlencoded</c> for <c>-d</c>; never null.
    /// </summary>
    /// <remarks>
    /// A <c>-H "Content-Type: ..."</c> or <c>-H "Content-Type:"</c> still overrides or
    /// removes it through <see cref="HttpRequestOptions.Headers" />.
    /// </remarks>
    public string ContentType { get; }
}
