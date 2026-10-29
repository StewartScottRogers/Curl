using System.Collections.Frozen;
using System.Net;
using System.Security.Authentication;
using System.Text;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Protocols;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;
using Curl.Cli;
using Curl.Engine;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using EasyFtpClearCommandChannel = Curl.Api.Options.Protocols.FtpClearCommandChannel;
using EasyFtpFileMethod = Curl.Api.Options.Protocols.FtpFileMethod;
using EasyGssApiDelegation = Curl.Api.Options.Authentication.GssApiDelegation;
using EasyTimeCondition = Curl.Api.Options.Behaviour.TimeCondition;
using HttpVersion = Curl.Api.Options.Http.HttpVersion;

namespace Curl.Console;

/// <summary>
/// The curl tool's <c>config2setopts.c</c>: maps one option group of a parsed command line onto the
/// easy option groups of <c>Curl.Api</c> (ADR-0506), one method per group, as curl hands its
/// options to libcurl with <c>curl_easy_setopt</c>. Each mapping agrees with the <c>CURLOPT_*</c>
/// lines <c>--libcurl</c> writes for the same command line, except where ADR-0513 to ADR-0516 record that the
/// command line's behaviour wins; an option the command line leaves out keeps libcurl's default.
/// </summary>
internal static class EasyOptionsFromCommandLine
{
    /// <summary>
    /// Every easy option group of one option group, as the runner sets them on a serial transfer's easy handle
    /// (ADR-0552), with <paramref name="url" /> as <c>CURLOPT_URL</c>; the callbacks keep libcurl's defaults, as the
    /// runner's attempt carries its own output, header, progress and event wiring.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <param name="url">The transfer's URL, or <see langword="null" /> when it is set afterwards with <c>curl_easy_setopt</c>.</param>
    /// <returns>The easy options.</returns>
    internal static Api.Options.EasyOptions EasyOptionsOf(CommandLineOptions options, string? url) =>
        new()
        {
            Behaviour = BehaviourOf(options),
            Transfer = TransferOf(options),
            Network = NetworkOf(options) with { Url = url },
            Tls = TlsOf(options),
            Authentication = AuthenticationOf(options),
            Proxy = ProxyOf(options),
            Http = HttpOf(options),
            Protocols = ProtocolOf(options),
        };

    /// <summary>
    /// The run's <c>--trace-config</c> components as the engine takes them: curl's <c>curl_global_trace</c> is a
    /// global, so no option group holds it, and the command line's <c>--trace-config</c>, <c>-v</c> and <c>--no-verbose</c>
    /// are all that set it (BL-2355).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The trace components that are on, and the ones only <c>-vv</c> and up put there.</returns>
    internal static EngineTraceComponents TraceComponentsOf(CommandLineOptions options) =>
        new(options.TraceComponents, options.VerbosityTraceComponents);

    // Each --upload-flags flag and the name it is sent under, in the fixed order curl 8.21.0 sends them
    // whatever order they were given in (BL-535 Notes).
    private static readonly (ImapUploadFlags Flag, string Name)[] UploadFlagNames =
    [
        (ImapUploadFlags.Answered, "answered"),
        (ImapUploadFlags.Deleted, "deleted"),
        (ImapUploadFlags.Draft, "draft"),
        (ImapUploadFlags.Flagged, "flagged"),
        (ImapUploadFlags.Seen, "seen"),
    ];

    // The names of the flags set in <flags>, in UploadFlagNames' fixed order, not the order given.
    private static string[] UploadFlagNamesOf(ImapUploadFlags flags) =>
        [.. UploadFlagNames.Where(pair => flags.HasFlag(pair.Flag)).Select(pair => pair.Name)];

    /// <summary>
    /// The curl tool's own settings the engine's transport factories read, which libcurl's easy options have no
    /// <c>CURLOPT_*</c> for: <c>-Z</c>, <c>--parallel-immediate</c>, <c>--ssl-sessions</c> and <c>-m</c> (BL-2356).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The tool options.</returns>
    internal static EngineToolOptions ToolOptionsOf(CommandLineOptions options) =>
        new(options.Parallel, options.ParallelImmediate, options.SslSessionsFile, options.MaxTime, TracesTransfer: options.Trace != TraceKind.None);

    /// <summary>
    /// The curl tool's own settings one transfer's setup and context read, which libcurl's easy options have no
    /// <c>CURLOPT_*</c> for or which the engine reads as the tool resolved them: <c>-s</c>, <c>-S</c>, <c>--fail</c>,
    /// <c>--create-file-mode</c>, <c>-k</c>, <c>--pass</c>, <c>--engine</c>, <c>--ech</c>, <c>-d</c>, <c>-U</c>,
    /// <c>--proto</c>, <c>--proto-redir</c>, the <c>--ssl*</c> level, <c>-l</c>, <c>--upload-flags</c>,
    /// <c>--proxy-header</c>, <c>-p</c>, <c>--unix-socket</c>, <c>--happy-eyeballs-timeout-ms</c> and the
    /// <c>-G</c> / <c>--url-query</c> query (BL-2357).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The transfer settings.</returns>
    internal static EngineTransferSettings TransferSettingsOf(CommandLineOptions options) =>
        new(
            options.Silent,
            options.ShowError,
            options.FailMode,
            options.CreateFileMode,
            options.Insecure,
            options.Passphrase,
            options.Engine,
            options.EchModeIsMalformed,
            options.PostData,
            options.ProxyCredentials,
            options.AllowedProtocols,
            options.AllowedRedirectProtocols,
            options.SslLevel,
            options.FtpSslControlOnly,
            options.SslRequired,
            options.ListOnly,
            UploadFlagNamesOf(options.UploadFlags),
            options.ProxyHeaders,
            options.ProxyTunnel,
            options.UnixSocketPath is not null,
            HttpRequestOptionsMapping.HappyEyeballsTimeoutOf(options.HappyEyeballsTimeout),
            url => QueryUrl.Append(url, options));

    // The receive buffer curl's tool asks for, 100 KiB, as --libcurl writes CURLOPT_BUFFERSIZE.
    private const long ToolBufferSize = 102400;

    // The type curl's tool gives a pkcs11: certificate or key without --cert-type or --key-type.
    private const string EnginePkcs11Type = "ENG";

    // The most whole seconds a TimeSpan holds. On Linux and macOS curl reads --keepalive-time and
    // --speed-time as a 64-bit long, past what a TimeSpan holds; such a wait never ends (CGR-039, BL-3568).
    private const long MostTimeSpanSeconds = long.MaxValue / TimeSpan.TicksPerSecond;

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

    // The kind a proxy option names, as the CURLPROXY_* --libcurl writes for it.
    private static readonly FrozenDictionary<ProxyKind, ProxyType> ProxyTypesByKind = new Dictionary<ProxyKind, ProxyType>
    {
        [ProxyKind.Http] = ProxyType.Http,
        [ProxyKind.Http10] = ProxyType.Http10,
        [ProxyKind.Https] = ProxyType.Https,
        [ProxyKind.Socks4] = ProxyType.Socks4,
        [ProxyKind.Socks4a] = ProxyType.Socks4A,
        [ProxyKind.Socks5] = ProxyType.Socks5,
        [ProxyKind.Socks5Hostname] = ProxyType.Socks5Hostname,
    }.ToFrozenDictionary();

    // The schemes the engine knows, as their CURLAUTH_* bits.
    private static readonly FrozenDictionary<HttpAuthSchemes, HttpAuthenticationMethods> AuthenticationMethodsByScheme = new Dictionary<HttpAuthSchemes, HttpAuthenticationMethods>
    {
        [HttpAuthSchemes.Basic] = HttpAuthenticationMethods.Basic,
        [HttpAuthSchemes.Digest] = HttpAuthenticationMethods.Digest,
        [HttpAuthSchemes.Ntlm] = HttpAuthenticationMethods.Ntlm,
        [HttpAuthSchemes.Negotiate] = HttpAuthenticationMethods.Negotiate,
        [HttpAuthSchemes.Bearer] = HttpAuthenticationMethods.Bearer,
    }.ToFrozenDictionary();

    // The versions the command line asks for, as the CURL_HTTP_VERSION_* curl's tool sets for each.
    private static readonly FrozenDictionary<RequestedHttpVersion, HttpVersion> HttpVersionsByRequested = new Dictionary<RequestedHttpVersion, HttpVersion>
    {
        [RequestedHttpVersion.Http10] = HttpVersion.Http10,
        [RequestedHttpVersion.Http11] = HttpVersion.Http11,
        [RequestedHttpVersion.Http2] = HttpVersion.Http2,
        [RequestedHttpVersion.Http2PriorKnowledge] = HttpVersion.Http2PriorKnowledge,
        [RequestedHttpVersion.Http3] = HttpVersion.Http3,
        [RequestedHttpVersion.Http3Only] = HttpVersion.Http3Only,
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

    // A --keepalive-time or --speed-time held to what a TimeSpan holds.
    internal static long AtMostTimeSpanSeconds(long seconds) => Math.Min(seconds, MostTimeSpanSeconds);

    // A --connect-timeout or -m held to the longest wait a .NET timer takes: on Linux and macOS curl takes
    // one longer, which ends no sooner than this (CGR-039, BL-3568).
    internal static TimeSpan AtMostLongestTimerWait(TimeSpan wait) =>
        wait < Core.MaxTimeWatchdog.LongestTimerWait ? wait : Core.MaxTimeWatchdog.LongestTimerWait;

    // The keepalive switch and timings. curl sets the timings only with keepalive on, and only those given.
    private static EasyNetworkOptions WithKeepAlive(CommandLineOptions options, EasyNetworkOptions network)
    {
        bool keepAliveTimeGiven = options.TcpKeepAlive && options.TcpKeepAliveSeconds > 0;
        return network with
        {
            TcpKeepAlive = options.TcpKeepAlive,
            TcpKeepIdle = keepAliveTimeGiven ? TimeSpan.FromSeconds(AtMostTimeSpanSeconds(options.TcpKeepAliveSeconds)) : network.TcpKeepIdle,
            TcpKeepInterval = keepAliveTimeGiven ? TimeSpan.FromSeconds(AtMostTimeSpanSeconds(options.TcpKeepAliveSeconds)) : network.TcpKeepInterval,
            TcpKeepCount = options.TcpKeepAlive && options.TcpKeepAliveProbeCount > 0 ? (int)Math.Min(options.TcpKeepAliveProbeCount, int.MaxValue) : network.TcpKeepCount,
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
            ConnectTimeout = options.ConnectTimeout is { } connect && connect > TimeSpan.Zero ? AtMostLongestTimerWait(connect) : network.ConnectTimeout,
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
    /// (split into the certificate and its pass phrase as curl's tool splits it), <c>--cert-type</c> (unset when not given, ADR-0530, but <c>ENG</c> for a <c>pkcs11:</c> certificate or key, ADR-0537),
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
            CertificateType = TypeOf(options.ClientCertificateType, certificate),
            Key = options.PrivateKey,
            KeyType = TypeOf(options.PrivateKeyType, options.PrivateKey),
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
            CertificateType = TypeOf(options.ProxyClientCertificateType, certificate),
            Key = options.ProxyPrivateKey,
            KeyType = TypeOf(options.ProxyPrivateKeyType, options.ProxyPrivateKey),
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

    // The --cert-type or --key-type given; without one, ENG for a pkcs11: certificate or key, as curl's tool sets it (BL-2349).
    private static string? TypeOf(string? type, string? certificateOrKey) =>
        type ?? (ClientCertificateArgument.IsPkcs11Uri(certificateOrKey) ? EnginePkcs11Type : null);

    // The minimum the command line names; none leaves the TLS library's own (the engine's behaviour, ADR-0514).
    private static SslVersion SslVersionOf(SslProtocols? minimum) =>
        SslVersionsByProtocol.GetValueOrDefault(minimum ?? SslProtocols.None, SslVersion.Default);

    // The ceiling --tls-max names; none (or default) leaves CURLOPT_SSLVERSION's high bits at 0.
    private static SslVersionMaximum SslVersionMaximumOf(SslProtocols? maximum) =>
        SslVersionMaximumsByProtocol.GetValueOrDefault(maximum ?? SslProtocols.None, SslVersionMaximum.None);

    private static SslOptions SslOptionOf(bool on, SslOptions bit) => on ? bit : SslOptions.None;

    /// <summary>
    /// Maps the proxy group: <c>-x</c>, <c>--proxy1.0</c>, <c>--socks4</c>, <c>--socks4a</c>,
    /// <c>--socks5</c>, <c>--socks5-hostname</c> and <c>--proxy-http2</c> (the proxy and its type),
    /// <c>--preproxy</c>, <c>--noproxy</c>, <c>-p</c>, <c>--suppress-connect-headers</c>,
    /// <c>--proxy-header</c>, <c>--socks5-basic</c>, <c>--socks5-gssapi</c>, <c>--socks5-gssapi-nec</c>,
    /// <c>--socks5-gssapi-service</c>, <c>--haproxy-protocol</c> and <c>--haproxy-clientip</c>. The proxy
    /// environment variables are libcurl's to read, so they are not part of the record; proxy credentials
    /// and proxy TLS belong to the authentication and TLS groups (ADR-0515).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <returns>The proxy options.</returns>
    public static EasyProxyOptions ProxyOf(CommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new EasyProxyOptions
        {
            Proxy = options.Proxy?.Address,
            ProxyType = ProxyTypeOf(options),
            PreProxy = options.PreProxy,
            NoProxy = options.NoProxy,
            HttpProxyTunnel = options.ProxyTunnel,
            SuppressConnectHeaders = options.SuppressConnectHeaders,
            ProxyHeaders = [.. options.ProxyHeaders],
            Socks5Authentication = Socks5AuthenticationOf(options),
            Socks5GssApiNec = options.Socks5GssapiNec,
            Socks5GssApiService = options.Socks5GssapiServiceName,
            HaProxyProtocol = options.HaproxyProtocol,
            HaProxyClientIp = options.HaproxyClientIp,
        };
    }

    // The option's kind, as --libcurl writes CURLOPT_PROXYTYPE; --proxy-http2 turns -x's HTTP into CURLPROXY_HTTPS2 (ADR-0515).
    private static ProxyType ProxyTypeOf(CommandLineOptions options) =>
        options.Proxy is not { } proxy ? ProxyType.Http
        : proxy.KindWithoutScheme == ProxyKind.Http && options.ProxyHttp2 ? ProxyType.Https2
        : ProxyTypesByKind[proxy.KindWithoutScheme];

    // curl's tool sets CURLOPT_SOCKS5_AUTH only when a --socks5-basic or --socks5-gssapi bit is on; none keeps libcurl's both.
    private static Socks5Authentication Socks5AuthenticationOf(CommandLineOptions options)
    {
        Socks5Authentication methods = (options.Socks5BasicAuth ? Socks5Authentication.Basic : Socks5Authentication.None)
            | (options.Socks5GssapiAuth ? Socks5Authentication.GssApi : Socks5Authentication.None);
        return methods == Socks5Authentication.None ? new EasyProxyOptions().Socks5Authentication : methods;
    }

    /// <summary>
    /// Maps the authentication group: <c>-u</c>, <c>-n</c>, <c>--netrc-file</c>, <c>--netrc-optional</c>,
    /// <c>--basic</c>, <c>--digest</c>, <c>--ntlm</c>, <c>--negotiate</c>, <c>--anyauth</c>, <c>--oauth2-bearer</c>,
    /// <c>--aws-sigv4</c>, <c>-U</c> and the <c>--proxy-*</c> method twins, <c>--login-options</c>,
    /// <c>--sasl-authzid</c>, <c>--sasl-ir</c>, <c>--service-name</c>, <c>--proxy-service-name</c>,
    /// <c>--delegation</c>, <c>--location-trusted</c>, <c>--disallow-username-in-url</c> and Curl's
    /// <c>--httpsig-*</c> options. <c>--ntlm-wb</c>, <c>--krb</c>, <c>--tlsauthtype</c>, <c>--tlsuser</c>,
    /// <c>--tlspassword</c> and their proxy twins have no function in the reference build, so the
    /// command line keeps no value for them and their members keep libcurl's default (ADR-0516).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <returns>The authentication options.</returns>
    public static EasyAuthenticationOptions AuthenticationOf(CommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // NetrcUse and Curl.Cli's GssApiDelegation hold libcurl's CURL_NETRC_* and CURLGSSAPI_DELEGATION_* values in the same order.
        return new EasyAuthenticationOptions
        {
            Netrc = (NetrcUsage)options.NetrcUse,
            NetrcFile = options.NetrcFile,
            UserPassword = UserPasswordOf(options.Credentials),
            LoginOptions = options.LoginOptions,
            HttpAuthentication = MethodsOf(options.AuthSchemes) | (options.AwsSigV4 is null ? HttpAuthenticationMethods.None : HttpAuthenticationMethods.AwsSigV4),
            ProxyAuthentication = MethodsOf(options.ProxyAuthSchemes),
            ProxyUserPassword = UserPasswordOf(options.ProxyCredentials),
            SaslAuthorizationIdentity = options.SaslAuthorizationIdentity,
            SaslInitialResponse = options.SaslInitialResponse,
            XOAuth2Bearer = options.BearerToken,
            DisallowUserNameInUrl = options.DisallowUsernameInUrl,
            UnrestrictedAuthentication = options.SendCredentialsToRedirectHosts,
            ServiceName = options.ServiceName,
            ProxyServiceName = options.ProxyServiceName,
            GssApiDelegation = (EasyGssApiDelegation)options.GssApiDelegation,
            AwsSigV4 = options.AwsSigV4,
            HttpSignatureAlgorithm = options.HttpSignatureAlgorithm,
            HttpSignatureKey = options.HttpSignatureKey,
            HttpSignatureKeyId = options.HttpSignatureKeyId,
            HttpSignatureHeaders = options.HttpSignatureHeaders,
        };
    }

    // user:password as --libcurl writes CURLOPT_USERPWD and CURLOPT_PROXYUSERPWD; the parser has already asked for a missing password.
    private static string? UserPasswordOf(NetworkCredential? credentials) =>
        credentials is null ? null : $"{credentials.UserName}:{credentials.Password}";

    // Every scheme the engine knows is CURLAUTH_ANY, as --anyauth asks libcurl; otherwise each scheme its CURLAUTH_* bit (ADR-0516).
    private static HttpAuthenticationMethods MethodsOf(HttpAuthSchemes schemes) =>
        (schemes & HttpAuthSchemes.Any) == HttpAuthSchemes.Any ? HttpAuthenticationMethods.Any
        : AuthenticationMethodsByScheme.Where(pair => (schemes & pair.Key) != 0).Aggregate(HttpAuthenticationMethods.None, (all, pair) => all | pair.Value);

    /// <summary>
    /// Maps the HTTP group: <c>-H</c> with the headers <c>--json</c> adds, <c>-A</c>, <c>-e</c> (and its
    /// <c>;auto</c>), <c>--compressed</c>, <c>--tr-encoding</c>, <c>--raw</c>, <c>-L</c>, <c>--follow</c>,
    /// <c>--max-redirs</c>, <c>--post301</c>, <c>--post302</c>, <c>--post303</c>, the <c>-d</c> family and
    /// <c>--json</c> body (none under <c>-G</c>, which moves it into the URL), the <c>-F</c> and <c>--form-string</c>
    /// parts as <see cref="EasyHttpOptions.MimePost" /> (ADR-0531), <c>--form-escape</c>,
    /// <c>--http1.0</c> to <c>--http3-only</c>, <c>--http0.9</c>, <c>--ignore-content-length</c>,
    /// <c>--expect100-timeout</c>, <c>--request-target</c>, <c>-b</c>, <c>-c</c>, <c>-j</c>, <c>--alt-svc</c>,
    /// <c>--hsts</c> and <c>--no-alpn</c>. <c>-X</c> and <c>-I</c> are the transfer group's, <c>--path-as-is</c>
    /// the network group's and the <c>--etag-compare</c> header is
    /// added per transfer, so none of them is set here (ADR-0527).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <returns>The HTTP options.</returns>
    public static EasyHttpOptions HttpOf(CommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        EasyHttpOptions http = WithRequest(options, WithEncodings(options, WithRedirects(options, new EasyHttpOptions())));
        return WithAltSvcAndHsts(options, WithCookies(options, WithBody(options, http)));
    }

    // The request line and headers.
    private static EasyHttpOptions WithRequest(CommandLineOptions options, EasyHttpOptions http) =>
        http with
        {
            HttpHeaders = HttpHeadersOf(options),
            UserAgent = options.UserAgent,
            Referer = options.Referer,
            AutoReferer = options.AutoReferer,
            HttpVersion = options.HttpVersion is { } version ? HttpVersionsByRequested[version] : HttpVersion.None,
            Http09Allowed = options.AllowHttp09Reply,
            RequestTarget = options.RequestTarget,
            Expect100Timeout = options.Expect100Timeout is { } wait && wait > TimeSpan.Zero ? wait : http.Expect100Timeout,
            SslEnableAlpn = options.UseAlpn,
        };

    // The encodings asked for and decoded; --raw turns both decodings off, as curl's tool does.
    private static EasyHttpOptions WithEncodings(CommandLineOptions options, EasyHttpOptions http) =>
        http with
        {
            AcceptEncoding = options.Compressed ? string.Empty : null,
            TransferEncoding = options.TransferEncoding,
            HttpContentDecoding = !options.Raw,
            HttpTransferDecoding = !options.Raw,
            IgnoreContentLength = options.IgnoreContentLength,
        };

    // -L is CURLFOLLOW_ALL and --follow CURLFOLLOW_OBEYCODE, as --libcurl writes 1L and 2L.
    private static EasyHttpOptions WithRedirects(CommandLineOptions options, EasyHttpOptions http) =>
        http with
        {
            FollowLocation = !options.FollowRedirects ? FollowLocation.None
                : options.FollowRedirectsPerSpec ? FollowLocation.ObeyCode
                : FollowLocation.All,
            MaxRedirects = options.MaxRedirects,
            PostRedirect = PostRedirectOf(options.KeepPostAfter301, PostRedirect.Post301)
                | PostRedirectOf(options.KeepPostAfter302, PostRedirect.Post302)
                | PostRedirectOf(options.KeepPostAfter303, PostRedirect.Post303),
        };

    // The -d family's bytes and their size as --libcurl writes them; -G leaves no body.
    private static EasyHttpOptions WithBody(CommandLineOptions options, EasyHttpOptions http) =>
        options.PostData is { } data && !options.DataInQuery
            ? http with { PostFields = data, PostFieldSize = data.Length, MimeOptions = MimeOptionsOf(options) }
            : http with { MimeOptions = MimeOptionsOf(options), MimePost = MimeBodyOf(options.FormParts) };

    // The -F and --form-string parts as curl's tool builds its curl_mime tree, or none without them (ADR-0531).
    private static EasyMimeBody? MimeBodyOf(IReadOnlyList<FormPartSpecification> parts) =>
        parts.Count == 0 ? null : new EasyMimeBody { Parts = [.. parts.Select(MimePartOf)] };

    // One -F part; a multipart part carries its nested parts as sub-parts, any other part carries its data.
    private static EasyMimePart MimePartOf(FormPartSpecification part) =>
        new()
        {
            Name = part.Name,
            FileName = MimeFileNameOf(part),
            Type = part.ContentType,
            Encoder = part.Encoder,
            Headers = part.Headers,
            SubParts = part.Kind == FormPartKind.Multipart ? MimeBodyOf(part.Parts) ?? new EasyMimeBody() : null,
            Data = MimeDataOf(part),
        };

    // curl_mime_filedata names an @file part after its path's base name unless ;filename= names it; <file clears the name.
    private static string? MimeFileNameOf(FormPartSpecification part) => part.Kind switch
    {
        FormPartKind.FileUpload => part.FileName ?? part.Content[(part.Content.LastIndexOfAny(['/', '\\']) + 1)..],
        FormPartKind.FileContent => null,
        _ => part.FileName,
    };

    // Text goes as UTF-8 bytes, a file by its path, and @- and <- as the tool's standard input callback.
    private static EasyMimeData? MimeDataOf(FormPartSpecification part) => part.Kind switch
    {
        FormPartKind.Text => new EasyMimeBytes(Encoding.UTF8.GetBytes(part.Content)),
        FormPartKind.Multipart => null,
        _ when part.Content == "-" => new EasyMimeStandardInput(),
        _ => new EasyMimeFile(part.Content),
    };

    // --form-escape switches the form's name and file name to CURLMIMEOPT_FORMESCAPE, which backslash-escapes a double quote in names instead of percent-encoding it.
    private static MimeOptions MimeOptionsOf(CommandLineOptions options) =>
        options.FormEscape ? MimeOptions.FormEscape : MimeOptions.None;

    // The -b strings joined as curl's cookie_setopts joins them for CURLOPT_COOKIE, the -b files, -c and -j.
    private static EasyHttpOptions WithCookies(CommandLineOptions options, EasyHttpOptions http)
    {
        string[] cookieStrings = CookieValuesOf(options, cookieStrings: true);
        return http with
        {
            Cookie = cookieStrings.Length > 0 ? JoinCookieStrings(cookieStrings) : null,
            CookieFiles = CookieValuesOf(options, cookieStrings: false),
            CookieJar = options.CookieJar,
            CookieSession = options.JunkSessionCookies,
        };
    }

    // The -b values that are cookie strings (they hold '=') when <cookieStrings> is true, else the ones that name cookie files.
    private static string[] CookieValuesOf(CommandLineOptions options, bool cookieStrings) =>
        [.. options.Cookies.Where(cookie => cookie.IsCookieString == cookieStrings).Select(cookie => cookie.Value)];

    // The first string as it is, each later one after ';' and a space, the space left out when the
    // string already starts with a space or a tab, as curl 8.21.0's cookie_setopts joins them (ADR-0533).
    private static string JoinCookieStrings(string[] cookieStrings)
    {
        StringBuilder joined = new(cookieStrings[0]);
        foreach (string cookieString in cookieStrings.Skip(1))
        {
            joined.Append(cookieString.StartsWith(' ') || cookieString.StartsWith('\t') ? ";" : "; ").Append(cookieString);
        }

        return joined.ToString();
    }

    // Setting CURLOPT_ALTSVC or CURLOPT_HSTS turns the cache on with libcurl's own control bits.
    private static EasyHttpOptions WithAltSvcAndHsts(CommandLineOptions options, EasyHttpOptions http) =>
        http with
        {
            AltSvc = options.AltSvcFile,
            AltSvcControl = options.AltSvcFile is null ? AltSvcControl.None : AltSvcControl.H1 | AltSvcControl.H2 | AltSvcControl.H3,
            Hsts = options.HstsFile,
            HstsControl = options.HstsFile is null ? HstsControl.None : HstsControl.Enable,
        };

    // The -H headers in order, then the --json ones no -H header already names, as curl's tool adds them.
    private static List<string> HttpHeadersOf(CommandLineOptions options)
    {
        List<string> headers = [.. options.Headers];
        if (options.SendsJson)
        {
            AddHeaderUnlessNamed(headers, "Content-Type: application/json");
            AddHeaderUnlessNamed(headers, "Accept: application/json");
        }

        return headers;
    }

    // Adds <header> unless a header with the same name (compared case-insensitively) is already there, as -H overrides --json's defaults.
    private static void AddHeaderUnlessNamed(List<string> headers, string header)
    {
        string nameAndColon = header[..(header.IndexOf(':', StringComparison.Ordinal) + 1)];
        if (!headers.Exists(existing => existing.StartsWith(nameAndColon, StringComparison.OrdinalIgnoreCase)))
        {
            headers.Add(header);
        }
    }

    private static PostRedirect PostRedirectOf(bool on, PostRedirect bit) => on ? bit : PostRedirect.None;

    /// <summary>
    /// Maps the behaviour group: <c>-v</c> and the <c>--trace</c> options (<c>CURLOPT_VERBOSE</c>;
    /// their output stays the tool's), <c>-i</c> and <c>-I</c> (headers in the body), <c>-s</c> and
    /// <c>--no-progress-meter</c> (<c>CURLOPT_NOPROGRESS</c>), <c>-f</c> (<c>--fail-with-body</c> is
    /// checked by the tool, not libcurl) and <c>--create-file-mode</c>. <c>-S</c>, <c>-#</c>,
    /// <c>-N</c>, <c>--retry*</c> and the output files never reach libcurl (ADR-0528).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <returns>The behaviour options.</returns>
    public static EasyBehaviourOptions BehaviourOf(CommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        EasyBehaviourOptions behaviour = new();
        return behaviour with
        {
            Verbose = options.Trace != TraceKind.None,
            IncludeHeader = options.ShowHeaders,
            NoProgress = options.Silent || options.ProgressMeterOff,
            FailOnError = options.FailMode == HttpFailMode.Fail,
            NewFilePermissions = options.CreateFileMode is { } mode && mode != 0 ? mode : behaviour.NewFilePermissions,
        };
    }

    /// <summary>
    /// Maps the transfer group: <c>-r</c>, <c>-C</c> with an offset, <c>-X</c>, <c>-R</c> and <c>-I</c>
    /// (the file time), <c>-l</c>, <c>-I</c> (no body), <c>--max-filesize</c>, <c>-z</c>, <c>-B</c>,
    /// <c>--crlf</c>, <c>-Y</c>, <c>-y</c>, <c>--limit-rate</c> (both speeds and the receive buffer) and
    /// <c>-m</c>. <c>-C -</c> and <c>-T</c> are per transfer: the offset comes from the output file and
    /// the upload from the URL's <c>-T</c> file, which <see cref="UploadTransferOf" /> adds (ADR-0528).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <returns>The transfer options.</returns>
    public static EasyTransferOptions TransferOf(CommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        EasyTransferOptions transfer = WithConditions(options, WithRequestShape(options, new EasyTransferOptions()));
        return WithRateAndTimeout(options, WithLowSpeed(options, transfer));
    }

    /// <summary>
    /// Adds a transfer's <c>-T</c> upload to its group's transfer options, as curl's tool sets
    /// <c>CURLOPT_UPLOAD</c> and <c>CURLOPT_INFILESIZE_LARGE</c> for the URL a <c>-T</c> file pairs with.
    /// </summary>
    /// <param name="transfer">The group's transfer options.</param>
    /// <param name="uploadFileSize">The upload's size in bytes; <see langword="null"/> when it is not known, as for standard input.</param>
    /// <returns>The transfer options with the upload.</returns>
    public static EasyTransferOptions UploadTransferOf(EasyTransferOptions transfer, long? uploadFileSize)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        return transfer with { Upload = true, InFileSize = uploadFileSize ?? transfer.InFileSize };
    }

    // What is asked for: the range, the -C offset, the method, the file time, names only and no body.
    private static EasyTransferOptions WithRequestShape(CommandLineOptions options, EasyTransferOptions transfer) =>
        transfer with
        {
            Range = options.Range,
            ResumeFrom = options.ResumeFrom ?? transfer.ResumeFrom,
            CustomRequest = options.RequestMethod,
            FileTime = options.NoBody || options.RemoteTime,
            DirListOnly = options.ListOnly,
            NoBody = options.NoBody,
            MaxFileSize = options.MaxFileSize ?? transfer.MaxFileSize,
        };

    // The -z condition, text mode and line-ending conversion.
    private static EasyTransferOptions WithConditions(CommandLineOptions options, EasyTransferOptions transfer) =>
        transfer with
        {
            TimeCondition = options.TimeCondition is not { } condition ? EasyTimeCondition.None
                : condition.SentAsLastModified ? EasyTimeCondition.LastModified
                : condition.Kind == TimeConditionKind.IfModifiedSince ? EasyTimeCondition.IfModifiedSince
                : EasyTimeCondition.IfUnmodifiedSince,
            TimeValue = options.TimeCondition?.ValueUnixSeconds ?? transfer.TimeValue,
            TransferText = options.UseAscii,
            Crlf = options.ConvertLineEndings,
        };

    // -Y alone also sets 30 seconds and -y alone a limit of 1, as --libcurl writes them.
    private static EasyTransferOptions WithLowSpeed(CommandLineOptions options, EasyTransferOptions transfer) =>
        transfer with
        {
            LowSpeedLimit = options.SpeedLimit ?? (options.SpeedTimeSeconds is null ? 0 : 1),
            LowSpeedTime = TimeSpan.FromSeconds(AtMostTimeSpanSeconds(options.SpeedTimeSeconds ?? (options.SpeedLimit is null ? 0 : 30))),
        };

    // --limit-rate both ways; the tool's receive buffer is 100 KiB, or a smaller --limit-rate; -m is set only when positive.
    private static EasyTransferOptions WithRateAndTimeout(CommandLineOptions options, EasyTransferOptions transfer)
    {
        long rate = options.LimitRate ?? 0;
        return transfer with
        {
            MaxSendSpeed = rate,
            MaxReceiveSpeed = rate,
            BufferSize = rate is > 0 and < ToolBufferSize ? rate : ToolBufferSize,
            Timeout = options.MaxTime is { } maxTime && maxTime > TimeSpan.Zero ? AtMostLongestTimerWait(maxTime) : transfer.Timeout,
        };
    }

    /// <summary>
    /// Maps the protocol group: FTP's <c>-P</c>, <c>-Q</c>, <c>-a</c>, <c>--disable-eprt</c>,
    /// <c>--disable-epsv</c>, <c>--ftp-pret</c>, <c>--ftp-create-dirs</c>, <c>--ftp-alternative-to-user</c>,
    /// <c>--ftp-skip-pasv-ip</c>, <c>--ftp-ssl-ccc</c>, <c>--ftp-ssl-ccc-mode</c>, <c>--ftp-account</c> and
    /// <c>--ftp-method</c>; SSH's <c>--key</c>, <c>--pubkey</c>, <c>--hostpubmd5</c>, <c>--hostpubsha256</c>,
    /// <c>--knownhosts</c> and <c>--compressed-ssh</c>; <c>--mail-from</c>, <c>--mail-rcpt</c>,
    /// <c>--mail-rcpt-allowfails</c> and <c>--mail-auth</c>; <c>--tftp-blksize</c> and
    /// <c>--tftp-no-options</c>; and <c>-t</c>. curl's tool sets no RTSP or WebSocket option of its own
    /// (an RTSP request is <c>-X</c>, the transfer group's), so those records keep libcurl's defaults;
    /// <c>-l</c>, <c>-B</c> and <c>--crlf</c> are the transfer group's, and the known-hosts file the tool
    /// finds without <c>--knownhosts</c> is per transfer (ADR-0529).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <returns>The protocol options.</returns>
    public static EasyProtocolOptions ProtocolOf(CommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new EasyProtocolOptions
        {
            Ftp = FtpOf(options),
            Ssh = SshOf(options),
            Smtp = SmtpOf(options),
            Tftp = TftpOf(options),
            Telnet = new EasyTelnetOptions { TelnetOptions = [.. options.TelnetOptions] },
        };
    }

    // --ftp-create-dirs is CURLFTP_CREATE_DIR_RETRY, as --libcurl writes 2L; --ftp-method's choice is libcurl's value less one.
    private static EasyFtpOptions FtpOf(CommandLineOptions options) =>
        new()
        {
            ActiveAddress = options.FtpPort,
            Quote = QuoteCommandsOf(options, prefix: null),
            PreQuote = QuoteCommandsOf(options, prefix: '+'),
            PostQuote = QuoteCommandsOf(options, prefix: '-'),
            Append = options.Append,
            UseEprt = options.FtpUseEprt,
            UseEpsv = !options.FtpDisableEpsv,
            UsePret = options.FtpSendPret,
            CreateMissingDirectories = options.FtpCreateDirectories ? FtpCreateMissingDirectories.CreateAndRetry : FtpCreateMissingDirectories.None,
            AlternativeToUser = options.FtpAlternativeToUser,
            SkipPasvIp = options.FtpSkipPasvIp,
            ClearCommandChannel = (EasyFtpClearCommandChannel)options.FtpClearCommandChannel,
            Account = options.FtpAccount,
            FileMethod = (EasyFtpFileMethod)((int)options.FtpFileMethod + 1),
        };

    // A -Q command starting with + runs just before the transfer and one with - after it, each without its prefix; any other before it starts.
    private static string[] QuoteCommandsOf(CommandLineOptions options, char? prefix) =>
        prefix is { } given
            ? [.. options.QuoteCommands.Where(command => command.StartsWith(given)).Select(command => command[1..])]
            : [.. options.QuoteCommands.Where(command => !command.StartsWith('+') && !command.StartsWith('-'))];

    // --key is both the TLS key and the SSH private key, as curl's tool sets both from it.
    private static EasySshOptions SshOf(CommandLineOptions options) =>
        new()
        {
            PrivateKeyFile = options.PrivateKey,
            PublicKeyFile = options.SshPublicKeyFile,
            HostPublicKeyMd5 = options.SshHostPublicKeyMd5,
            HostPublicKeySha256 = options.SshHostPublicKeySha256,
            Compression = options.SshCompression,
            KnownHostsFile = options.SshKnownHostsFile,
        };

    // The --mail-* options as CURLOPT_MAIL_FROM, MAIL_RCPT, MAIL_AUTH and MAIL_RCPT_ALLOWFAILS take them.
    private static EasySmtpOptions SmtpOf(CommandLineOptions options) =>
        new()
        {
            MailFrom = options.MailFrom,
            MailRecipients = [.. options.MailRecipients],
            MailAuth = options.MailAuth,
            MailRecipientAllowFails = options.MailRecipientAllowFails,
        };

    // --tftp-blksize as given; the TFTP handler clamps it as libcurl does.
    private static EasyTftpOptions TftpOf(CommandLineOptions options)
    {
        EasyTftpOptions tftp = new();
        return tftp with { BlockSize = options.TftpBlockSize ?? tftp.BlockSize, NoOptions = options.TftpNoOptions };
    }

    // The schemes as CURLOPT_PROTOCOLS_STR takes them: lowercase, sorted, joined by commas.
    private static string? ProtocolListOf(IReadOnlySet<string>? schemes) =>
        schemes is null ? null : string.Join(",", schemes.Order(StringComparer.Ordinal));
}
