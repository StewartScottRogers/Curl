using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// The request an <see cref="IHttpAuthenticator" /> is asked to authorise, and what it may
/// authorise it with (ADR-0014).
/// </summary>
/// <param name="Method">
/// The method of the request being authorised, as sent; Digest hashes it.
/// </param>
/// <param name="Url">The URL of the request being authorised.</param>
/// <param name="RequestTarget">
/// The request line's target as sent: origin form, absolute form, or
/// <see cref="HttpRequestOptions.RequestTarget" />. Digest's <c>uri=</c> field is this.
/// </param>
/// <param name="Credential">
/// For the origin, <see cref="ITransferContext.Credentials" />; for a proxy, the proxy's
/// credential. <see langword="null" /> when there is none.
/// </param>
/// <param name="BearerToken">
/// <see cref="HttpRequestOptions.BearerToken" /> for the origin; always
/// <see langword="null" /> for a proxy.
/// </param>
/// <param name="AllowedSchemes">
/// For the origin, <see cref="HttpRequestOptions.AuthSchemes" />; for a proxy,
/// <see cref="HttpAuthSchemes.Basic" />, curl's default.
/// </param>
/// <param name="IsProxy">
/// <see langword="true" /> when answering a proxy: the challenges are
/// <c>Proxy-Authenticate</c> values and the result is sent as <c>Proxy-Authorization</c>.
/// <see langword="false" /> for the origin: <c>WWW-Authenticate</c> and
/// <c>Authorization</c>.
/// </param>
public sealed record HttpAuthRequest(
    string Method,
    CurlUrl Url,
    string RequestTarget,
    NetworkCredential? Credential,
    string? BearerToken,
    HttpAuthSchemes AllowedSchemes,
    bool IsProxy)
{
    /// <summary>
    /// Gets where the authenticator reports the <c>-v</c> lines answering the request causes,
    /// such as a Negotiate context's failure (BL-843); <see cref="NoTransferEvents.Instance" />
    /// when nobody is listening. The HTTP handler decides where in the transfer's output they
    /// land.
    /// </summary>
    public ITransferEvents Events { get; init; } = NoTransferEvents.Instance;

    /// <summary>
    /// Gets the DER of the TLS server certificate of the connection the request goes over;
    /// empty, the default, when it has no TLS. Negotiate turns it into channel bindings (BL-915).
    /// </summary>
    public ReadOnlyMemory<byte> ServerCertificate { get; init; }

    /// <summary>
    /// Gets what signing the request with AWS Signature Version 4 needs, which curl does in
    /// place of every other scheme when <c>--aws-sigv4</c> is given; <see langword="null" />,
    /// the default, when it is not, and always for a proxy (BL-629).
    /// </summary>
    public AwsSigV4Inputs? AwsSigV4 { get; init; }
}
