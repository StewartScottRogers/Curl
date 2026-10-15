using System.Net.Sockets;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;
using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// What one <c>-:</c>/<c>--next</c> option group opens its connections with, beyond what the
/// <see cref="ConnectTarget" /> names: a connection an earlier group left in the run's
/// <see cref="ConnectionCache" /> is reused by a later group only when the two groups' settings are
/// equal, as curl 8.21.0 reuses one only when <c>Curl_ssl_config_matches</c> and its other
/// connection checks agree (ADR-0285, BL-754). The <c>--connect-to</c> values are not compared
/// here: the pool key holds the host and port a mapping dials (<c>ConnectionPoolKey.ConnectToAuthority</c>),
/// so groups whose mappings reach the same place share a connection, as curl 8.22.0's do (test2051, BL-2164).
/// </summary>
/// <param name="Tls">The origin's TLS options (<see cref="TlsClientOptionsMapping.Of" />).</param>
/// <param name="ProxyTls">The HTTPS proxy's TLS options (<see cref="TlsClientOptionsMapping.ProxyOf" />).</param>
/// <param name="UnixSocket">The <c>--unix-socket</c> or <c>--abstract-unix-socket</c>, or <see langword="null" />.</param>
/// <param name="LocalBinding">The <c>--interface</c> and <c>--local-port</c> binding, or <see langword="null" />.</param>
/// <param name="AddressFamily">The <c>-4</c>/<c>-6</c> choice.</param>
/// <param name="HaproxyProtocol">The PROXY protocol line's settings, or <see langword="null" />.</param>
/// <param name="PreProxy">The <c>--preproxy</c>, or <see langword="null" />.</param>
/// <param name="ApplicationProtocols">What HTTP over TLS offers through ALPN, one per line.</param>
internal sealed record OptionGroupConnectionSettings(
    TlsClientOptions Tls,
    TlsClientOptions ProxyTls,
    UnixSocketAddress? UnixSocket,
    LocalBinding? LocalBinding,
    AddressFamily AddressFamily,
    HaproxyProtocolHeader? HaproxyProtocol,
    ProxyEndpoint? PreProxy,
    string ApplicationProtocols)
{
    /// <summary>Reads the settings of one option group.</summary>
    /// <param name="options">The option group, for its ALPN and <c>--ssl-sessions</c> options.</param>
    /// <param name="network">The option group's network options, for its socket, binding and IP version.</param>
    /// <param name="tls">The option group's TLS options, for the target's and the HTTPS proxy's handshakes.</param>
    /// <param name="http">The option group's HTTP options, for the HTTP version the ALPN list follows.</param>
    /// <param name="proxy">The option group's proxy options, for its pre-proxy and HAProxy line.</param>
    /// <returns>The settings.</returns>
    internal static OptionGroupConnectionSettings Of(CommandLineOptions options, EasyNetworkOptions network, EasyTlsOptions tls, EasyHttpOptions http, EasyProxyOptions proxy) =>
        new(
            TlsClientOptionsMapping.Of(tls, options.UseAlpn, options.SslSessionsFile),
            TlsClientOptionsMapping.ProxyOf(tls, options.UseAlpn),
            EngineComposition.UnixSocketOf(network),
            EngineComposition.LocalBindingOf(network),
            EngineComposition.AddressFamilyOf(network),
            EngineComposition.HaproxyProtocolOf(proxy),
            EngineComposition.PreProxyOf(proxy),
            string.Join('\n', HttpVersionMapping.HttpOverTlsApplicationProtocolsOf(http.HttpVersion)));
}
