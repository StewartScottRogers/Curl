using System.Collections.Frozen;
using System.Security.Authentication;
using Curl.Api.Options.Network;
using Curl.Api.Options.Tls;
using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The curl tool's <c>config2setopts.c</c>: maps one option group of a parsed command line onto the
/// easy option groups of <c>Curl.Api</c> (ADR-0506), one method per group, as curl hands its
/// options to libcurl with <c>curl_easy_setopt</c>. Each mapping agrees with the <c>CURLOPT_*</c>
/// lines <c>--libcurl</c> writes for the same command line, except where ADR-0513 and ADR-0514 record that the
/// command line's behaviour wins; an option the command line leaves out keeps libcurl's default.
/// </summary>
internal static class EasyOptionsFromCommandLine
{
    private const string Pkcs11UriScheme = "pkcs11:";

    // The versions the parser records, as SslProtocols, and the CURL_SSLVERSION_* each is as a minimum.
    private static readonly FrozenDictionary<SslProtocols, SslVersion> SslVersionsByProtocol = new Dictionary<SslProtocols, SslVersion>
    {
        [ObsoleteTlsProtocols.Tls10] = SslVersion.Tls10,
        [ObsoleteTlsProtocols.Tls11] = SslVersion.Tls11,
        [SslProtocols.Tls12] = SslVersion.Tls12,
        [SslProtocols.Tls13] = SslVersion.Tls13,
    }.ToFrozenDictionary();

    // The same versions as the CURL_SSLVERSION_MAX_* each is as a ceiling.
    private static readonly FrozenDictionary<SslProtocols, SslVersionMaximum> SslVersionMaximumsByProtocol = new Dictionary<SslProtocols, SslVersionMaximum>
    {
        [ObsoleteTlsProtocols.Tls10] = SslVersionMaximum.Tls10,
        [ObsoleteTlsProtocols.Tls11] = SslVersionMaximum.Tls11,
        [SslProtocols.Tls12] = SslVersionMaximum.Tls12,
        [SslProtocols.Tls13] = SslVersionMaximum.Tls13,
    }.ToFrozenDictionary();

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

    /// <summary>
    /// Maps the TLS group: the origin's <c>-k</c>, <c>--cacert</c>, <c>--capath</c>, <c>-E</c>
    /// (split into the certificate and its pass phrase as curl's tool splits it), <c>--cert-type</c>,
    /// <c>--key</c>, <c>--key-type</c>, <c>--pass</c>, <c>--ciphers</c>, <c>--tls13-ciphers</c>,
    /// <c>--tlsv1*</c>, <c>--tls-max</c>, <c>--crlfile</c>, <c>--pinnedpubkey</c>, <c>--ssl-no-revoke</c>,
    /// <c>--ssl-revoke-best-effort</c>, <c>--ssl-allow-beast</c>, <c>--ssl-auto-client-cert</c>,
    /// <c>--ca-native</c> and <c>--tls-earlydata</c>; their <c>--proxy-*</c> twins and
    /// <c>--proxy-tlsv1</c>; and <c>--curves</c>, <c>--sigalgs</c>, <c>--cert-status</c>,
    /// <c>--no-sessionid</c>, <c>--ech</c>, <c>--engine</c>, <c>-w</c> (which gathers the certificate
    /// chain, as <c>--libcurl</c> writes <c>CURLOPT_CERTINFO</c> for it), <c>--doh-insecure</c> and
    /// <c>--doh-cert-status</c>. <c>--sslv2</c>, <c>--sslv3</c> and <c>--false-start</c> have no
    /// function, and <c>--ssl-sessions</c> is the tool's own file, not an option (ADR-0514).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <returns>The TLS options.</returns>
    public static EasyTlsOptions TlsOf(CommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new EasyTlsOptions
        {
            Origin = OriginTlsOf(options),
            Proxy = ProxyTlsOf(options),
            Engine = options.Engine,
            VerifyStatus = options.RequireCertificateStatus,
            GatherCertificateInfo = options.WriteOut is not null,
            EllipticCurves = options.Curves,
            SignatureAlgorithms = options.SignatureAlgorithms,
            SessionIdCache = options.ReuseSessionIds,
            EncryptedClientHello = options.Ech,
            EncryptedClientHelloPublicName = options.EchPublicName,
            EncryptedClientHelloConfigList = options.EchConfigList,
            DohVerifyPeer = !options.DohInsecure,
            DohVerifyHost = !options.DohInsecure,
            DohVerifyStatus = options.DohCertificateStatus,
        };
    }

    // The origin's TLS options. -E's own pass phrase gives way to --pass, as --libcurl writes CURLOPT_KEYPASSWD.
    private static TlsPeerOptions OriginTlsOf(CommandLineOptions options)
    {
        (string? certificate, string? passphrase) = CertificateOf(options.ClientCertificate);
        return new TlsPeerOptions
        {
            Certificate = certificate,
            CertificateType = FileTypeOf(options.ClientCertificateType, certificate),
            Key = options.PrivateKey,
            KeyType = FileTypeOf(options.PrivateKeyType, options.PrivateKey),
            KeyPassword = options.Passphrase ?? passphrase,
            SslVersion = SslVersionOf(options.MinimumTlsVersion),
            SslVersionMaximum = SslVersionMaximumOf(options.MaximumTlsVersion),
            VerifyPeer = !options.Insecure,
            VerifyHost = !options.Insecure,
            CertificateAuthorityFile = options.CaCertificateFile,
            CertificateAuthorityPath = options.CaCertificateDirectory,
            RevocationListFile = options.CertificateRevocationListFile,
            PinnedPublicKey = options.PinnedPublicKey,
            CipherList = options.Ciphers,
            Tls13Ciphers = options.Tls13Ciphers,
            SslOptions = SslOptionOf(options.AllowBeast, SslOptions.AllowBeast) | SslOptionOf(options.SkipRevocationCheck, SslOptions.NoRevoke)
                | SslOptionOf(options.RevocationCheckBestEffort, SslOptions.RevokeBestEffort) | SslOptionOf(options.UseNativeCaStore, SslOptions.NativeCa)
                | SslOptionOf(options.AutoClientCertificate, SslOptions.AutoClientCert) | SslOptionOf(options.TlsEarlyData, SslOptions.EarlyData),
        };
    }

    // An HTTPS proxy's TLS options. --capath reaches the proxy without --proxy-capath; -k, --cacert and --tls-max never do.
    private static TlsPeerOptions ProxyTlsOf(CommandLineOptions options)
    {
        (string? certificate, string? passphrase) = CertificateOf(options.ProxyClientCertificate);
        return new TlsPeerOptions
        {
            Certificate = certificate,
            CertificateType = FileTypeOf(options.ProxyClientCertificateType, certificate),
            Key = options.ProxyPrivateKey,
            KeyType = FileTypeOf(options.ProxyPrivateKeyType, options.ProxyPrivateKey),
            KeyPassword = options.ProxyPassphrase ?? passphrase,
            SslVersion = options.ProxyMinimumTlsVersion is null ? SslVersion.Default : SslVersion.Tls1,
            VerifyPeer = !options.ProxyInsecure,
            VerifyHost = !options.ProxyInsecure,
            CertificateAuthorityFile = options.ProxyCaCertificateFile,
            CertificateAuthorityPath = options.ProxyCaCertificateDirectory ?? options.CaCertificateDirectory,
            RevocationListFile = options.ProxyCertificateRevocationListFile,
            PinnedPublicKey = options.ProxyPinnedPublicKey,
            CipherList = options.ProxyCiphers,
            Tls13Ciphers = options.ProxyTls13Ciphers,
            SslOptions = SslOptionOf(options.ProxyAllowBeast, SslOptions.AllowBeast) | SslOptionOf(options.ProxyUseNativeCaStore, SslOptions.NativeCa)
                | SslOptionOf(options.ProxyAutoClientCertificate, SslOptions.AutoClientCert),
        };
    }

    // -E's certificate[:passphrase], split as curl's tool splits it; only its Windows build keeps a drive letter's colon.
    private static (string? Certificate, string? Passphrase) CertificateOf(string? value) =>
        value is null ? (null, null) : ClientCertificateArgument.Split(value, OperatingSystem.IsWindows());

    // The type given, else ENG for a pkcs11: certificate or key as curl's tool sets it, else libcurl's PEM.
    private static string FileTypeOf(string? type, string? file) =>
        type ?? (file is not null && file.StartsWith(Pkcs11UriScheme, StringComparison.OrdinalIgnoreCase) ? "ENG" : "PEM");

    // The minimum the command line names; none leaves the TLS library's own (the engine's behaviour, ADR-0514).
    private static SslVersion SslVersionOf(SslProtocols? minimum) =>
        SslVersionsByProtocol.GetValueOrDefault(minimum ?? SslProtocols.None, SslVersion.Default);

    // The ceiling --tls-max names; none (or default) leaves CURLOPT_SSLVERSION's high bits at 0.
    private static SslVersionMaximum SslVersionMaximumOf(SslProtocols? maximum) =>
        SslVersionMaximumsByProtocol.GetValueOrDefault(maximum ?? SslProtocols.None, SslVersionMaximum.None);

    private static SslOptions SslOptionOf(bool on, SslOptions bit) => on ? bit : SslOptions.None;

    // The schemes as CURLOPT_PROTOCOLS_STR takes them: lowercase, sorted, joined by commas.
    private static string? ProtocolListOf(IReadOnlySet<string>? schemes) =>
        schemes is null ? null : string.Join(",", schemes.Order(StringComparer.Ordinal));
}
