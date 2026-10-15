using System.Collections.Frozen;
using System.Net;
using System.Security.Authentication;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;
using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using EasyGssApiDelegation = Curl.Api.Options.Authentication.GssApiDelegation;
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
    /// <c>--json</c> body (none under <c>-G</c>, which moves it into the URL), <c>--form-escape</c>,
    /// <c>--http1.0</c> to <c>--http3-only</c>, <c>--http0.9</c>, <c>--ignore-content-length</c>,
    /// <c>--expect100-timeout</c>, <c>--request-target</c>, <c>-b</c>, <c>-c</c>, <c>-j</c>, <c>--alt-svc</c>,
    /// <c>--hsts</c> and <c>--no-alpn</c>. <c>-X</c> and <c>-I</c> are the transfer group's, <c>--path-as-is</c>
    /// the network group's, the <c>-F</c> body is mapped by BL-2344 and the <c>--etag-compare</c> header is
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
            : http with { MimeOptions = MimeOptionsOf(options) };

    private static MimeOptions MimeOptionsOf(CommandLineOptions options) =>
        options.FormEscape ? MimeOptions.FormEscape : MimeOptions.None;

    // The -b strings joined as --libcurl writes CURLOPT_COOKIE, the -b files, -c and -j.
    private static EasyHttpOptions WithCookies(CommandLineOptions options, EasyHttpOptions http)
    {
        string[] cookieStrings = CookieValuesOf(options, cookieStrings: true);
        return http with
        {
            Cookie = cookieStrings.Length > 0 ? string.Join("; ", cookieStrings) : null,
            CookieFiles = CookieValuesOf(options, cookieStrings: false),
            CookieJar = options.CookieJar,
            CookieSession = options.JunkSessionCookies,
        };
    }

    private static string[] CookieValuesOf(CommandLineOptions options, bool cookieStrings) =>
        [.. options.Cookies.Where(cookie => cookie.IsCookieString == cookieStrings).Select(cookie => cookie.Value)];

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

    private static void AddHeaderUnlessNamed(List<string> headers, string header)
    {
        string nameAndColon = header[..(header.IndexOf(':', StringComparison.Ordinal) + 1)];
        if (!headers.Exists(existing => existing.StartsWith(nameAndColon, StringComparison.OrdinalIgnoreCase)))
        {
            headers.Add(header);
        }
    }

    private static PostRedirect PostRedirectOf(bool on, PostRedirect bit) => on ? bit : PostRedirect.None;

    // The schemes as CURLOPT_PROTOCOLS_STR takes them: lowercase, sorted, joined by commas.
    private static string? ProtocolListOf(IReadOnlySet<string>? schemes) =>
        schemes is null ? null : string.Join(",", schemes.Order(StringComparer.Ordinal));
}
