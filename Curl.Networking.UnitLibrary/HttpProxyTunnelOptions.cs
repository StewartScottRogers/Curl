using System.Text;

namespace Curl.Networking;

/// <summary>
/// What <see cref="TcpConnector" /> puts in the CONNECT request that opens a tunnel
/// through an HTTP proxy: the <c>User-Agent</c> and the encoding of the proxy credential.
/// </summary>
/// <param name="UserAgent">
/// The <c>User-Agent</c> header value, as <c>-A</c>/<c>--user-agent</c> sets it for the
/// transfer, or <see langword="null" /> to send no <c>User-Agent</c> header.
/// </param>
/// <param name="CredentialEncoding">
/// The encoding <c>user:password</c> is turned into bytes with before it is base64-encoded
/// into <c>Proxy-Authorization: Basic</c>, the same encoding the server credential uses
/// (ADR-0022).
/// </param>
public sealed record HttpProxyTunnelOptions(string? UserAgent, Encoding CredentialEncoding)
{
    /// <summary>
    /// Gets the options curl 8.21.0 uses when no option changes them: <c>User-Agent:
    /// curl/8.21.0</c> and UTF-8 credentials.
    /// </summary>
    public static HttpProxyTunnelOptions Default { get; } = new("curl/8.21.0", Encoding.UTF8);
}
