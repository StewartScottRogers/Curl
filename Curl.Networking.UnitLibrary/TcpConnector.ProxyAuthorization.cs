using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    /// <summary>
    /// Asks <see cref="HttpProxyTunnelOptions.ProxyAuthenticator" /> for the CONNECT's
    /// <c>Proxy-Authorization</c> to <paramref name="destination" />: with no challenges for the
    /// first CONNECT, with a <c>407</c>'s <c>Proxy-Authenticate</c> values after one. The
    /// authenticator reports its <c>-v</c> lines, such as a Negotiate context's failure, to
    /// <paramref name="authenticatorEvents" />.
    /// </summary>
    private ValueTask<string?> CreateProxyAuthorizationAsync(
        ConnectDestination destination,
        ProxyEndpoint proxy,
        IReadOnlyList<string> challenges,
        ITransferEvents authenticatorEvents,
        CancellationToken cancellationToken) =>
        ProxyAuthenticator.CreateAuthorizationAsync(ProxyAuthRequestOf(destination, proxy) with { Events = authenticatorEvents }, challenges, cancellationToken);

    /// <summary>
    /// The authenticator that answers the proxy: <see cref="HttpProxyTunnelOptions.ProxyAuthenticator" />,
    /// or pre-emptive Basic without one.
    /// </summary>
    private IHttpAuthenticator ProxyAuthenticator =>
        _proxyTunnelOptions.ProxyAuthenticator ?? new PreemptiveBasicProxyAuthenticator(_proxyTunnelOptions.CredentialEncoding);

    /// <summary>
    /// The request the authenticator is asked about for a CONNECT to <paramref name="destination" />:
    /// the proxy's own URL, so NTLM and Negotiate ask for <c>HTTP</c> on the proxy's host as
    /// curl 8.21.0 does (BL-604 Notes), and the CONNECT's authority as the request target.
    /// </summary>
    private HttpAuthRequest ProxyAuthRequestOf(ConnectDestination destination, ProxyEndpoint proxy)
    {
        var scheme = proxy.Kind == ProxyKind.Https ? "https" : "http";
        return new HttpAuthRequest(
            "CONNECT",
            CurlUrl.Parse($"{scheme}://{HttpProxyTunnel.FormatAuthority(proxy.Host, proxy.Port)}/"),
            HttpProxyTunnel.FormatAuthority(destination.Host, destination.Port),
            proxy.Credential,
            null,
            _proxyTunnelOptions.ProxyAuthSchemes,
            IsProxy: true);
    }

    /// <summary>
    /// Tells the authenticator the handshake behind <paramref name="sentAuthorization" /> is
    /// over because the tunnel opened, so a Negotiate context kept for its next leg is disposed
    /// of, as the HTTP handler does for a response that is not a <c>407</c> (ADR-0248).
    /// </summary>
    private void EndProxyAuthorization(string? sentAuthorization)
    {
        if (sentAuthorization is not null)
        {
            ProxyAuthenticator.EndAuthorization(sentAuthorization);
        }
    }
}
