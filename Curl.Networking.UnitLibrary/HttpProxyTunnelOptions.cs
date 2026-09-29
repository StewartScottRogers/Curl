using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// What <see cref="TcpConnector" /> puts in the CONNECT request that opens a tunnel
/// through an HTTP proxy: the <c>User-Agent</c>, the encoding of the proxy credential,
/// the <c>--proxy-header</c> values (ADR-0077) and how the proxy is authenticated to.
/// </summary>
/// <param name="UserAgent">
/// The <c>User-Agent</c> header value, as <c>-A</c>/<c>--user-agent</c> sets it for the
/// transfer, or <see langword="null" /> to send no <c>User-Agent</c> header.
/// </param>
/// <param name="CredentialEncoding">
/// The encoding <c>user:password</c> is turned into bytes with before it is base64-encoded
/// into <c>Proxy-Authorization: Basic</c> by the default
/// <see cref="ProxyAuthenticator" />, the same encoding the server credential uses
/// (ADR-0022).
/// </param>
public sealed record HttpProxyTunnelOptions(string? UserAgent, Encoding CredentialEncoding)
{
    /// <summary>
    /// Gets the options curl 8.21.0 uses when no option changes them: <c>User-Agent:
    /// curl/8.21.0</c> and UTF-8 credentials.
    /// </summary>
    public static HttpProxyTunnelOptions Default { get; } = new("curl/8.21.0", Encoding.UTF8);

    /// <summary>
    /// Gets the <c>--proxy-header</c> values, verbatim and in command-line order: each one
    /// that names a header replaces curl's own CONNECT header of that name, and each one
    /// that sends a line is appended after curl's own headers (BL-347). Empty by default.
    /// </summary>
    public IReadOnlyList<string> ProxyHeaders { get; init; } = [];

    /// <summary>
    /// Gets the encoding the <c>User-Agent</c> and <c>--proxy-header</c> text is turned into
    /// bytes with, the platform curl's argument encoding (ADR-0067). Latin-1 by default,
    /// where a character above U+00FF takes Latin-1's best fit or else <c>?</c>.
    /// </summary>
    public Encoding CommandLineTextEncoding { get; init; } = Encoding.Latin1;

    /// <summary>
    /// Gets the schemes the proxy may be authenticated with, as <c>--proxy-basic</c>,
    /// <c>--proxy-digest</c> and <c>--proxy-anyauth</c> pick them (BL-601).
    /// <see cref="HttpAuthSchemes.Basic" />, libcurl's default, unless set.
    /// </summary>
    public HttpAuthSchemes ProxyAuthSchemes { get; init; } = HttpAuthSchemes.Basic;

    /// <summary>
    /// Gets what answers for the proxy credential: asked with no challenge for the first
    /// CONNECT's <c>Proxy-Authorization</c>, and with the <c>Proxy-Authenticate</c> values
    /// of a <c>407</c> to a CONNECT that sent none (ADR-0186). <see langword="null" />, the
    /// default, answers Basic before any challenge and nothing after one, in
    /// <see cref="CredentialEncoding" /> (<see cref="PreemptiveBasicProxyAuthenticator" />).
    /// </summary>
    public IHttpAuthenticator? ProxyAuthenticator { get; init; }
}
