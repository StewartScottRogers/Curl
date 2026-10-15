using Curl.Api.Options.Network;
using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The curl tool's <c>config2setopts.c</c>: maps one option group of a parsed command line onto the
/// easy option groups of <c>Curl.Api</c> (ADR-0506), one method per group, as curl hands its
/// options to libcurl with <c>curl_easy_setopt</c>. Each mapping agrees with the <c>CURLOPT_*</c>
/// lines <c>--libcurl</c> writes for the same command line, except where ADR-0513 records that the
/// command line's behaviour wins; an option the command line leaves out keeps libcurl's default.
/// </summary>
internal static class EasyOptionsFromCommandLine
{
    /// <summary>
    /// Maps the network group: <c>--connect-to</c>, <c>--resolve</c>, <c>--interface</c>,
    /// <c>--local-port</c>, <c>--dns-servers</c>, <c>--dns-interface</c>, <c>--dns-ipv4-addr</c>,
    /// <c>--dns-ipv6-addr</c>, <c>--doh-url</c>, <c>-4</c>/<c>-6</c>, <c>--tcp-nodelay</c>,
    /// <c>--tcp-fastopen</c>, <c>--keepalive-time</c>, <c>--keepalive-cnt</c>, <c>--no-keepalive</c>,
    /// <c>--ip-tos</c>, <c>--vlan-priority</c>, <c>--mptcp</c>, <c>--unix-socket</c>,
    /// <c>--abstract-unix-socket</c>, <c>--connect-timeout</c>, <c>--happy-eyeballs-timeout-ms</c>,
    /// <c>--path-as-is</c>, <c>--proto</c>, <c>--proto-redir</c>, <c>--proto-default</c> and
    /// <c>--ssl</c>, <c>--ssl-reqd</c> and <c>--ftp-ssl-control</c>. The URL is not part of an option
    /// group, so <see cref="EasyNetworkOptions.Url" /> stays unset.
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <returns>The network options.</returns>
    public static EasyNetworkOptions NetworkOf(CommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        EasyNetworkOptions network = WithNameResolution(options, WithAddressing(options, new EasyNetworkOptions()));
        return WithTimeoutsAndSecurity(options, WithSockets(options, WithKeepAlive(options, network)));
    }

    // What to reach and from where: the URL handling, protocols, local binding and mappings.
    private static EasyNetworkOptions WithAddressing(CommandLineOptions options, EasyNetworkOptions network) =>
        network with
        {
            PathAsIs = options.PathAsIs,
            Protocols = ProtocolListOf(options.AllowedProtocols),
            RedirectProtocols = ProtocolListOf(options.AllowedRedirectProtocols),
            DefaultProtocol = options.DefaultProtocol,
            Interface = options.Interface?.Value,
            LocalPort = options.LocalPorts is { } ports ? ports.First : network.LocalPort,
            LocalPortRange = options.LocalPorts is { } range ? range.Count : network.LocalPortRange,
            ConnectTo = [.. options.ConnectToEntries],
            Resolve = [.. options.ResolveEntries],
        };

    // How names resolve: the DNS servers and their binding, DoH and the IP version.
    private static EasyNetworkOptions WithNameResolution(CommandLineOptions options, EasyNetworkOptions network) =>
        network with
        {
            DnsServers = options.DnsServers,
            DnsInterface = options.DnsInterface,
            DnsLocalIPv4 = options.DnsIPv4Address,
            DnsLocalIPv6 = options.DnsIPv6Address,
            DohUrl = options.DohUrl,
            IpResolve = options.IpAddressFamily switch
            {
                IpAddressFamilyChoice.IPv4Only => IpResolve.V4,
                IpAddressFamilyChoice.IPv6Only => IpResolve.V6,
                _ => IpResolve.Whatever,
            },
        };

    // The keepalive switch and timings. curl sets the timings only with keepalive on, and only those given.
    private static EasyNetworkOptions WithKeepAlive(CommandLineOptions options, EasyNetworkOptions network)
    {
        bool keepAliveTimeGiven = options.TcpKeepAlive && options.TcpKeepAliveSeconds > 0;
        return network with
        {
            TcpKeepAlive = options.TcpKeepAlive,
            TcpKeepIdle = keepAliveTimeGiven ? TimeSpan.FromSeconds(options.TcpKeepAliveSeconds) : network.TcpKeepIdle,
            TcpKeepInterval = keepAliveTimeGiven ? TimeSpan.FromSeconds(options.TcpKeepAliveSeconds) : network.TcpKeepInterval,
            TcpKeepCount = options.TcpKeepAlive && options.TcpKeepAliveProbeCount > 0 ? (int)options.TcpKeepAliveProbeCount : network.TcpKeepCount,
        };
    }

    // The other socket options and the Unix domain socket.
    private static EasyNetworkOptions WithSockets(CommandLineOptions options, EasyNetworkOptions network) =>
        network with
        {
            TcpNoDelay = options.TcpNoDelay,
            TcpFastOpen = options.TcpFastOpen,
            IpTypeOfService = options.IpTypeOfService,
            VlanPriority = options.VlanPriority,
            MultipathTcp = options.MultipathTcp,
            UnixSocketPath = options.UnixSocketIsAbstract ? null : options.UnixSocketPath,
            AbstractUnixSocket = options.UnixSocketIsAbstract ? options.UnixSocketPath : null,
        };

    // The connect phase's timeouts, which curl sets only when positive, and whether TLS is required.
    private static EasyNetworkOptions WithTimeoutsAndSecurity(CommandLineOptions options, EasyNetworkOptions network) =>
        network with
        {
            ConnectTimeout = options.ConnectTimeout is { } connect && connect > TimeSpan.Zero ? connect : network.ConnectTimeout,
            HappyEyeballsTimeout = options.HappyEyeballsTimeout is { } eyeballs && eyeballs > TimeSpan.Zero ? eyeballs : network.HappyEyeballsTimeout,
            UseSsl = UseSslOf(options),
        };

    // --ftp-ssl-control outranks --ssl-reqd, as the command line reads them (ADR-0513), which outranks --ssl.
    private static UseSsl UseSslOf(CommandLineOptions options) =>
        options.FtpSslControlOnly ? UseSsl.Control
        : options.SslRequired ? UseSsl.All
        : options.SslLevel == TransportSecurityLevel.Try ? UseSsl.Try
        : UseSsl.None;

    // The schemes as CURLOPT_PROTOCOLS_STR takes them: lowercase, sorted, joined by commas.
    private static string? ProtocolListOf(IReadOnlySet<string>? schemes) =>
        schemes is null ? null : string.Join(",", schemes.Order(StringComparer.Ordinal));
}
