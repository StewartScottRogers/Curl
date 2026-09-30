namespace Curl.Protocol.Abstractions;

/// <summary>
/// What an <see cref="IHttpAuthenticator" /> needs beyond an <see cref="HttpAuthRequest" />'s
/// method, URL and credential to sign the request with AWS Signature Version 4, as
/// <c>--aws-sigv4</c> asks (BL-629, ADR-0243): the parameter, the <c>Host</c> value, the
/// <c>-H</c> headers and the body, as the HTTP handler sends them.
/// </summary>
/// <param name="Parameter">
/// The <c>--aws-sigv4</c> value, <c>provider1[:provider2[:region[:service]]]</c>, verbatim
/// (<see cref="HttpRequestOptions.AwsSigV4" />).
/// </param>
/// <param name="HostHeaderValue">The value of the <c>Host</c> header curl's own head carries, e.g. <c>127.0.0.1:18629</c>.</param>
/// <param name="CustomHeaders">The <c>-H</c> headers, each exactly as given (<see cref="HttpRequestOptions.Headers" />).</param>
public sealed record AwsSigV4Inputs(string Parameter, string HostHeaderValue, IReadOnlyList<string> CustomHeaders)
{
    /// <summary>
    /// Gets the in-memory body from <c>-d</c> and its kin (<see cref="BytesBody" />), or
    /// <see langword="null" />, the default, when the request has none: it is hashed as the payload.
    /// </summary>
    public ReadOnlyMemory<byte>? PostFields { get; init; }

    /// <summary>
    /// Gets the size of the <c>-T</c> upload; -1, the default, when its size is unknown or
    /// nothing is uploaded.
    /// </summary>
    public long UploadSize { get; init; } = -1;

    /// <summary>
    /// Gets a value indicating whether the request is sent as a GET or a HEAD: no body and no
    /// upload, whatever <c>-X</c> puts on the request line.
    /// </summary>
    public bool IsGetOrHead { get; init; }

    /// <summary>Gets a value indicating whether <c>--path-as-is</c> is set, which curl refuses to sign.</summary>
    public bool PathAsIs { get; init; }
}
