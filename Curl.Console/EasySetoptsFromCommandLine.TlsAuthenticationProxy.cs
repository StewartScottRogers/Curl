using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;
using Libcurl.Options;

namespace Curl.Console;

/// <summary>
/// The TLS, authentication and proxy half of <see cref="EasySetoptsFromCommandLine" /> (BL-2492, ADR-0642): the
/// <c>curl_easy_setopt</c> calls <c>config2setopts.c</c> makes for those groups, and what of them stays on the hooks.
/// </summary>
internal static partial class EasySetoptsFromCommandLine
{
    // The CURLOPT_SSL_VERIFYHOST value curl's tool sets for a check that is on.
    private const long VerifyHostOn = 2;

    private static readonly HashSet<string> EncryptedClientHelloModes = new(StringComparer.Ordinal) { "false", "grease", "true", "hard" };

    private static readonly EasyTlsOptions UnsetTls = new();

    private static readonly EasyAuthenticationOptions UnsetAuthentication = new();

    private static readonly EasyProxyOptions UnsetProxy = new();

    private static readonly SetoptRow<EasyTlsOptions>[] OriginTlsRows = PeerRows(
        t => t.Origin,
        [CurlOption.Sslcert, CurlOption.SslcertBlob, CurlOption.Sslcerttype, CurlOption.Sslkey, CurlOption.SslkeyBlob, CurlOption.Sslkeytype, CurlOption.Keypasswd,
         CurlOption.Sslversion, CurlOption.SslVerifypeer, CurlOption.SslVerifyhost, CurlOption.Cainfo, CurlOption.CainfoBlob, CurlOption.Capath, CurlOption.Crlfile,
         CurlOption.Issuercert, CurlOption.IssuercertBlob, CurlOption.Pinnedpublickey, CurlOption.SslCipherList, CurlOption.Tls13Ciphers, CurlOption.SslOptions]);

    private static readonly SetoptRow<EasyTlsOptions>[] ProxyTlsRows = PeerRows(
        t => t.Proxy,
        [CurlOption.ProxySslcert, CurlOption.ProxySslcertBlob, CurlOption.ProxySslcerttype, CurlOption.ProxySslkey, CurlOption.ProxySslkeyBlob, CurlOption.ProxySslkeytype, CurlOption.ProxyKeypasswd,
         CurlOption.ProxySslversion, CurlOption.ProxySslVerifypeer, CurlOption.ProxySslVerifyhost, CurlOption.ProxyCainfo, CurlOption.ProxyCainfoBlob, CurlOption.ProxyCapath, CurlOption.ProxyCrlfile,
         CurlOption.ProxyIssuercert, CurlOption.ProxyIssuercertBlob, CurlOption.ProxyPinnedpublickey, CurlOption.ProxySslCipherList, CurlOption.ProxyTls13Ciphers, CurlOption.ProxySslOptions]);

    private static readonly SetoptRow<EasyTlsOptions>[] HandleTlsRows =
    [
        new(CurlOption.SslVerifystatus, t => FlagOf(t.VerifyStatus)),
        new(CurlOption.Certinfo, t => FlagOf(t.GatherCertificateInfo)),
        new(CurlOption.SslEcCurves, t => t.EllipticCurves),
        new(CurlOption.SslSignatureAlgorithms, t => t.SignatureAlgorithms),
        new(CurlOption.SslSessionidCache, t => FlagOf(t.SessionIdCache)),
        new(CurlOption.Ech, t => t.EncryptedClientHello, IsSettableEchMode),
        new(CurlOption.Ech, t => EchValueOf("pn:", t.EncryptedClientHelloPublicName), t => IsSettableEchValue(t.EncryptedClientHelloPublicName)),
        new(CurlOption.Ech, t => EchValueOf("ecl:", t.EncryptedClientHelloConfigList), t => IsSettableEchValue(t.EncryptedClientHelloConfigList)),
        new(CurlOption.DohSslVerifypeer, t => FlagOf(t.DohVerifyPeer)),
        new(CurlOption.DohSslVerifyhost, t => VerifyHostOf(t.DohVerifyHost)),
        new(CurlOption.DohSslVerifystatus, t => FlagOf(t.DohVerifyStatus)),
    ];

    // CURLOPT_AWS_SIGV4 comes before CURLOPT_HTTPAUTH: its setter also sets HTTPAUTH, which the record's own then replaces.
    private static readonly SetoptRow<EasyAuthenticationOptions>[] AuthenticationRows =
    [
        new(CurlOption.Netrc, a => (long)a.Netrc),
        new(CurlOption.NetrcFile, a => a.NetrcFile),
        new(CurlOption.Userpwd, a => a.UserPassword),
        new(CurlOption.Username, a => a.UserName),
        new(CurlOption.Password, a => a.Password),
        new(CurlOption.LoginOptions, a => a.LoginOptions),
        new(CurlOption.AwsSigv4, a => a.AwsSigV4),
        new(CurlOption.Httpauth, a => (long)a.HttpAuthentication),
        new(CurlOption.Proxyauth, a => (long)a.ProxyAuthentication),
        new(CurlOption.Proxyuserpwd, a => a.ProxyUserPassword),
        new(CurlOption.Proxyusername, a => a.ProxyUserName),
        new(CurlOption.Proxypassword, a => a.ProxyPassword),
        new(CurlOption.SaslAuthzid, a => a.SaslAuthorizationIdentity),
        new(CurlOption.SaslIr, a => FlagOf(a.SaslInitialResponse)),
        new(CurlOption.Xoauth2Bearer, a => a.XOAuth2Bearer),
        new(CurlOption.DisallowUsernameInUrl, a => FlagOf(a.DisallowUserNameInUrl)),
        new(CurlOption.UnrestrictedAuth, a => FlagOf(a.UnrestrictedAuthentication)),
        new(CurlOption.ServiceName, a => a.ServiceName),
        new(CurlOption.ProxyServiceName, a => a.ProxyServiceName),
        new(CurlOption.GssapiDelegation, a => (long)a.GssApiDelegation),
        new(CurlOption.Krblevel, a => a.KerberosLevel),
    ];

    // CURLOPT_HAPROXY_CLIENT_IP comes before CURLOPT_HAPROXYPROTOCOL: its setter turns the protocol on, as libcurl's does.
    private static readonly SetoptRow<EasyProxyOptions>[] ProxyRows =
    [
        new(CurlOption.Proxy, p => p.Proxy),
        new(CurlOption.Proxyport, p => (long)p.ProxyPort),
        new(CurlOption.Proxytype, p => (long)p.ProxyType),
        new(CurlOption.PreProxy, p => p.PreProxy),
        new(CurlOption.Noproxy, p => p.NoProxy),
        new(CurlOption.Httpproxytunnel, p => FlagOf(p.HttpProxyTunnel)),
        new(CurlOption.SuppressConnectHeaders, p => FlagOf(p.SuppressConnectHeaders)),
        new(CurlOption.Proxyheader, p => ListOf(p.ProxyHeaders)),
        new(CurlOption.Headeropt, p => (long)p.HeaderOption),
        new(CurlOption.Socks5Auth, p => (long)p.Socks5Authentication),
        new(CurlOption.Socks5GssapiNec, p => FlagOf(p.Socks5GssApiNec)),
        new(CurlOption.Socks5GssapiService, p => p.Socks5GssApiService),
        new(CurlOption.HaproxyClientIp, p => p.HaProxyClientIp),
        new(CurlOption.Haproxyprotocol, p => FlagOf(p.HaProxyProtocol)),
    ];

    private static IEnumerable<EasySetopt> TlsAuthenticationAndProxySetoptsOf(EasyOptions options) =>
        SetoptsOf(OriginTlsRows, options.Tls, UnsetTls)
            .Concat(SetoptsOf(ProxyTlsRows, options.Tls, UnsetTls))
            .Concat(SetoptsOf(HandleTlsRows, options.Tls, UnsetTls))
            .Concat(SetoptsOf(AuthenticationRows, options.Authentication, UnsetAuthentication))
            .Concat(SetoptsOf(ProxyRows, options.Proxy, UnsetProxy));

    // --engine (CURLOPT_SSLENGINE answers CURLE_NOT_BUILT_IN; the engine's own setup failure prints curl's text), the
    // engine default and False Start (both CURLE_NOT_BUILT_IN), the CA cache timeout (the command line never moves it), and
    // an --ech value CURLOPT_ECH would ignore stay on the hooks.
    private static EasyTlsOptions TlsKeptOnHooks(EasyTlsOptions tls) =>
        UnsetTls with
        {
            Engine = tls.Engine,
            EngineIsDefault = tls.EngineIsDefault,
            FalseStart = tls.FalseStart,
            CertificateAuthorityCacheTimeout = tls.CertificateAuthorityCacheTimeout,
            EncryptedClientHello = IsSettableEchMode(tls) ? UnsetTls.EncryptedClientHello : tls.EncryptedClientHello,
            EncryptedClientHelloPublicName = IsSettableEchValue(tls.EncryptedClientHelloPublicName) ? UnsetTls.EncryptedClientHelloPublicName : tls.EncryptedClientHelloPublicName,
            EncryptedClientHelloConfigList = IsSettableEchValue(tls.EncryptedClientHelloConfigList) ? UnsetTls.EncryptedClientHelloConfigList : tls.EncryptedClientHelloConfigList,
        };

    // Curl's --httpsig-* options have no CURLOPT_*, and the TLS-SRP options' setter refuses any type but SRP.
    private static EasyAuthenticationOptions AuthenticationKeptOnHooks(EasyAuthenticationOptions authentication) =>
        UnsetAuthentication with
        {
            TlsAuthType = authentication.TlsAuthType,
            TlsAuthUserName = authentication.TlsAuthUserName,
            TlsAuthPassword = authentication.TlsAuthPassword,
            ProxyTlsAuthType = authentication.ProxyTlsAuthType,
            ProxyTlsAuthUserName = authentication.ProxyTlsAuthUserName,
            ProxyTlsAuthPassword = authentication.ProxyTlsAuthPassword,
            HttpSignatureAlgorithm = authentication.HttpSignatureAlgorithm,
            HttpSignatureKey = authentication.HttpSignatureKey,
            HttpSignatureKeyId = authentication.HttpSignatureKeyId,
            HttpSignatureHeaders = authentication.HttpSignatureHeaders,
        };

    // The twenty options libcurl has once for the origin and once with PROXY_, in TlsPeerOptions' order.
    private static SetoptRow<EasyTlsOptions>[] PeerRows(Func<EasyTlsOptions, TlsPeerOptions> peerOf, CurlOption[] options) =>
    [
        new(options[0], t => peerOf(t).Certificate),
        new(options[1], t => peerOf(t).CertificateBlob),
        new(options[2], t => peerOf(t).CertificateType),
        new(options[3], t => peerOf(t).Key),
        new(options[4], t => peerOf(t).KeyBlob),
        new(options[5], t => peerOf(t).KeyType),
        new(options[6], t => peerOf(t).KeyPassword),
        new(options[7], t => (long)peerOf(t).SslVersion | (long)peerOf(t).SslVersionMaximum),
        new(options[8], t => FlagOf(peerOf(t).VerifyPeer)),
        new(options[9], t => VerifyHostOf(peerOf(t).VerifyHost)),
        new(options[10], t => peerOf(t).CertificateAuthorityFile),
        new(options[11], t => peerOf(t).CertificateAuthorityBlob),
        new(options[12], t => peerOf(t).CertificateAuthorityPath),
        new(options[13], t => peerOf(t).RevocationListFile),
        new(options[14], t => peerOf(t).IssuerCertificate),
        new(options[15], t => peerOf(t).IssuerCertificateBlob),
        new(options[16], t => peerOf(t).PinnedPublicKey),
        new(options[17], t => peerOf(t).CipherList),
        new(options[18], t => peerOf(t).Tls13Ciphers),
        new(options[19], t => (long)peerOf(t).SslOptions),
    ];

    private static long VerifyHostOf(bool on) => on ? VerifyHostOn : 0L;

    // CURLOPT_ECH keeps only its four modes; any other mode text changes nothing.
    private static bool IsSettableEchMode(EasyTlsOptions tls) => tls.EncryptedClientHello is null || EncryptedClientHelloModes.Contains(tls.EncryptedClientHello);

    // CURLOPT_ECH keeps a pn: or ecl: value only when at least two characters follow the prefix.
    private static bool IsSettableEchValue(string? value) => value is null || value.Length >= 2;

    private static string? EchValueOf(string prefix, string? value) => value is null ? null : prefix + value;
}
