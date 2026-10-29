using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Tls;
using static Libcurl.Options.TlsPeerOptionSetters;

namespace Libcurl.Options;

/// <summary>
/// The setter rows of every <c>CURLOPT_*</c> <see cref="EasyTlsOptions"/> holds: the origin's, their
/// <c>PROXY_*</c> twins, the origin-only options and the <c>DOH_SSL_*</c> options, each checking its value
/// as curl 8.22.0's <c>lib/setopt.c</c> does (ADR-0580). <c>CURLOPT_SSL_CTX_FUNCTION</c> and
/// <c>CURLOPT_SSL_CTX_DATA</c> are the callback group's.
/// </summary>
internal static class EasyTlsOptionSetters
{
    /// <summary>The most seconds <c>CURLOPT_CA_CACHE_TIMEOUT</c> keeps: libcurl stores it in an <see cref="int"/> and clamps larger values.</summary>
    private const long LargestCacheTimeoutSeconds = int.MaxValue;

    /// <summary>The whole-string <c>CURLOPT_ECH</c> values that set the mode; the <c>ecl:</c> and <c>pn:</c> forms are matched by prefix.</summary>
    private static readonly HashSet<string> EncryptedClientHelloModes = new(StringComparer.Ordinal) { "false", "grease", "true", "hard" };

    /// <summary>
    /// Whether this platform's curl takes <c>CURLOPT_TLS13_CIPHERS</c> and <c>CURLOPT_PROXY_TLS13_CIPHERS</c>: a Schannel
    /// build (Windows) has no TLS 1.3 cipher suite selection and answers <see cref="CurlCode.NotBuiltIn"/> (ADR-0991).
    /// </summary>
    internal static bool Tls13CiphersIsBuiltIn { get; set; } = !OperatingSystem.IsWindows();

    /// <summary>
    /// Whether this platform's curl takes <c>CURLOPT_PROXY_CAPATH</c>: a Schannel build (Windows) answers
    /// <see cref="CurlCode.NotBuiltIn"/> for it while taking <c>CURLOPT_CAPATH</c> (ADR-0991).
    /// </summary>
    internal static bool ProxyCaPathIsBuiltIn { get; set; } = !OperatingSystem.IsWindows();

    /// <summary>Whether this platform's TLS backend has <c>SSLSUPP_CRLFILE</c> for <c>CURLOPT_CRLFILE</c> and its <c>PROXY_</c> twin: Schannel (Windows) lacks it (ADR-0995).</summary>
    internal static bool RevocationListFileIsBuiltIn { get; set; } = !OperatingSystem.IsWindows();

    /// <summary>Whether this platform's TLS backend has <c>SSLSUPP_ISSUERCERT</c> and <c>_BLOB</c> for <c>CURLOPT_ISSUERCERT</c>, its blob and their <c>PROXY_</c> twins: Schannel (Windows) lacks them (ADR-0995).</summary>
    internal static bool IssuerCertificateIsBuiltIn { get; set; } = !OperatingSystem.IsWindows();

    /// <summary>Whether this platform's TLS backend has <c>SSLSUPP_SSL_EC_CURVES</c> for <c>CURLOPT_SSL_EC_CURVES</c>: Schannel (Windows) lacks it (ADR-0995).</summary>
    internal static bool EllipticCurvesIsBuiltIn { get; set; } = !OperatingSystem.IsWindows();

    /// <summary>Whether this platform's TLS backend has <c>SSLSUPP_SIGNATURE_ALGORITHMS</c> for <c>CURLOPT_SSL_SIGNATURE_ALGORITHMS</c>: Schannel (Windows) lacks it (ADR-0995).</summary>
    internal static bool SignatureAlgorithmsIsBuiltIn { get; set; } = !OperatingSystem.IsWindows();

    /// <summary>Whether this platform's TLS backend has <c>cert_status_request</c> for <c>CURLOPT_SSL_VERIFYSTATUS</c> and <c>CURLOPT_DOH_SSL_VERIFYSTATUS</c>: on everywhere, Curl checking stapled OCSP responses itself where Schannel would not (ADR-0995).</summary>
    internal static bool CertificateStatusIsBuiltIn { get; set; } = true;

    /// <summary>Whether this build has <c>USE_ECH</c> for <c>CURLOPT_ECH</c>: on everywhere, Curl doing ECH itself where the platform's curl would not (ADR-0995).</summary>
    internal static bool EncryptedClientHelloIsBuiltIn { get; set; } = true;

    /// <summary>
    /// Whether this platform's TLS backend has OpenSSL's engine and provider entry points: off (Schannel, Windows) an engine
    /// name and <c>CURLOPT_SSLENGINE_DEFAULT</c> answer <see cref="CurlCode.NotBuiltIn"/>; on, Curl having no engine or
    /// provider to load, a name answers <see cref="CurlCode.SslEngineNotFound"/> and the default <see cref="CurlCode.Ok"/> (ADR-0995).
    /// </summary>
    internal static bool OpenSslEngineIsBuiltIn { get; set; } = !OperatingSystem.IsWindows();

    /// <summary>Adds the TLS rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        AddOriginSetters(setters, Origin);
        AddProxySetters(setters, Proxy);
        AddHandleSetters(setters);
    }

    /// <summary>Adds the rows for the origin's TLS options, from <paramref name="peer"/>.</summary>
    private static void AddOriginSetters(Dictionary<CurlOption, EasyOptionSetter> setters, TlsPeerOptionSetters peer)
    {
        setters[CurlOption.Sslcert] = new(peer.SetCertificate);
        setters[CurlOption.SslcertBlob] = new(peer.SetCertificateBlob);
        setters[CurlOption.Sslcerttype] = new(peer.SetCertificateType);
        setters[CurlOption.Sslkey] = new(peer.SetKey);
        setters[CurlOption.SslkeyBlob] = new(peer.SetKeyBlob);
        setters[CurlOption.Sslkeytype] = new(peer.SetKeyType);
        setters[CurlOption.Keypasswd] = new(peer.SetKeyPassword);
        setters[CurlOption.Sslversion] = new(peer.SetSslVersion);
        setters[CurlOption.SslVerifypeer] = new(peer.SetVerifyPeer);
        setters[CurlOption.SslVerifyhost] = new(peer.SetVerifyHost);
        setters[CurlOption.Cainfo] = new(peer.SetCertificateAuthorityFile);
        setters[CurlOption.CainfoBlob] = new(peer.SetCertificateAuthorityBlob);
        setters[CurlOption.Capath] = new(peer.SetCertificateAuthorityPath);
        setters[CurlOption.Crlfile] = new(SetOriginRevocationListFile);
        setters[CurlOption.Issuercert] = new(SetOriginIssuerCertificate);
        setters[CurlOption.IssuercertBlob] = new(SetOriginIssuerCertificateBlob);
        setters[CurlOption.Pinnedpublickey] = new(peer.SetPinnedPublicKey);
        setters[CurlOption.SslCipherList] = new(peer.SetCipherList);
        setters[CurlOption.Tls13Ciphers] = new(SetOriginTls13Ciphers);
        setters[CurlOption.SslOptions] = new(peer.SetSslOptions);
    }

    /// <summary>Adds the <c>PROXY_*</c> twin of each origin row, from <paramref name="peer"/>.</summary>
    private static void AddProxySetters(Dictionary<CurlOption, EasyOptionSetter> setters, TlsPeerOptionSetters peer)
    {
        setters[CurlOption.ProxySslcert] = new(peer.SetCertificate);
        setters[CurlOption.ProxySslcertBlob] = new(peer.SetCertificateBlob);
        setters[CurlOption.ProxySslcerttype] = new(peer.SetCertificateType);
        setters[CurlOption.ProxySslkey] = new(peer.SetKey);
        setters[CurlOption.ProxySslkeyBlob] = new(peer.SetKeyBlob);
        setters[CurlOption.ProxySslkeytype] = new(peer.SetKeyType);
        setters[CurlOption.ProxyKeypasswd] = new(peer.SetKeyPassword);
        setters[CurlOption.ProxySslversion] = new(peer.SetSslVersion);
        setters[CurlOption.ProxySslVerifypeer] = new(peer.SetVerifyPeer);
        setters[CurlOption.ProxySslVerifyhost] = new(peer.SetVerifyHost);
        setters[CurlOption.ProxyCainfo] = new(peer.SetCertificateAuthorityFile);
        setters[CurlOption.ProxyCainfoBlob] = new(peer.SetCertificateAuthorityBlob);
        setters[CurlOption.ProxyCapath] = new(SetProxyCertificateAuthorityPath);
        setters[CurlOption.ProxyCrlfile] = new(SetProxyRevocationListFile);
        setters[CurlOption.ProxyIssuercert] = new(SetProxyIssuerCertificate);
        setters[CurlOption.ProxyIssuercertBlob] = new(SetProxyIssuerCertificateBlob);
        setters[CurlOption.ProxyPinnedpublickey] = new(peer.SetPinnedPublicKey);
        setters[CurlOption.ProxySslCipherList] = new(peer.SetCipherList);
        setters[CurlOption.ProxyTls13Ciphers] = new(SetProxyTls13Ciphers);
        setters[CurlOption.ProxySslOptions] = new(peer.SetSslOptions);
    }

    /// <summary>Adds the rows with no <c>PROXY_*</c> twin: the cache, ECH, DoH and engine options.</summary>
    private static void AddHandleSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Sslengine] = new(SetEngine);
        setters[CurlOption.SslengineDefault] = new(SetEngineDefault);
        setters[CurlOption.SslVerifystatus] = new(SetVerifyStatus);
        setters[CurlOption.CaCacheTimeout] = new(SetCertificateAuthorityCacheTimeout);
        setters[CurlOption.Certinfo] = new(SetGatherCertificateInfo);
        setters[CurlOption.SslEcCurves] = new(SetEllipticCurves);
        setters[CurlOption.SslSignatureAlgorithms] = new(SetSignatureAlgorithms);
        setters[CurlOption.SslSessionidCache] = new(SetSessionIdCache);
        setters[CurlOption.SslFalsestart] = new(SetFalseStart);
        setters[CurlOption.Ech] = new(SetEncryptedClientHello);
        setters[CurlOption.DohSslVerifypeer] = new(SetDohVerifyPeer);
        setters[CurlOption.DohSslVerifyhost] = new(SetDohVerifyHost);
        setters[CurlOption.DohSslVerifystatus] = new(SetDohVerifyStatus);
        setters[CurlOption.SslEnableNpn] = new(IgnoreDeprecated);
        setters[CurlOption.Egdsocket] = new(IgnoreDeprecated);
        setters[CurlOption.RandomFile] = new(IgnoreDeprecated);
    }

    /// <summary><c>CURLOPT_TLS13_CIPHERS</c>: the origin's TLS 1.3 cipher suites, or <see cref="CurlCode.NotBuiltIn"/> where <see cref="Tls13CiphersIsBuiltIn"/> is off.</summary>
    private static CurlCode SetOriginTls13Ciphers(ref EasyOptions o, object? v) => Tls13CiphersIsBuiltIn ? Origin.SetTls13Ciphers(ref o, v) : CurlCode.NotBuiltIn;

    /// <summary><c>CURLOPT_PROXY_TLS13_CIPHERS</c>: the proxy's TLS 1.3 cipher suites, or <see cref="CurlCode.NotBuiltIn"/> where <see cref="Tls13CiphersIsBuiltIn"/> is off.</summary>
    private static CurlCode SetProxyTls13Ciphers(ref EasyOptions o, object? v) => Tls13CiphersIsBuiltIn ? Proxy.SetTls13Ciphers(ref o, v) : CurlCode.NotBuiltIn;

    /// <summary><c>CURLOPT_PROXY_CAPATH</c>: the proxy's CA directory, or <see cref="CurlCode.NotBuiltIn"/> where <see cref="ProxyCaPathIsBuiltIn"/> is off.</summary>
    private static CurlCode SetProxyCertificateAuthorityPath(ref EasyOptions o, object? v) =>
        ProxyCaPathIsBuiltIn ? Proxy.SetCertificateAuthorityPath(ref o, v) : CurlCode.NotBuiltIn;

    /// <summary><c>CURLOPT_CRLFILE</c>: the origin's CRL file, or <see cref="CurlCode.NotBuiltIn"/> where <see cref="RevocationListFileIsBuiltIn"/> is off.</summary>
    private static CurlCode SetOriginRevocationListFile(ref EasyOptions o, object? v) =>
        RevocationListFileIsBuiltIn ? Origin.SetRevocationListFile(ref o, v) : CurlCode.NotBuiltIn;

    /// <summary><c>CURLOPT_PROXY_CRLFILE</c>: the proxy's CRL file, or <see cref="CurlCode.NotBuiltIn"/> where <see cref="RevocationListFileIsBuiltIn"/> is off.</summary>
    private static CurlCode SetProxyRevocationListFile(ref EasyOptions o, object? v) =>
        RevocationListFileIsBuiltIn ? Proxy.SetRevocationListFile(ref o, v) : CurlCode.NotBuiltIn;

    /// <summary><c>CURLOPT_ISSUERCERT</c>: the origin's issuer certificate, or <see cref="CurlCode.NotBuiltIn"/> where <see cref="IssuerCertificateIsBuiltIn"/> is off.</summary>
    private static CurlCode SetOriginIssuerCertificate(ref EasyOptions o, object? v) =>
        IssuerCertificateIsBuiltIn ? Origin.SetIssuerCertificate(ref o, v) : CurlCode.NotBuiltIn;

    /// <summary><c>CURLOPT_PROXY_ISSUERCERT</c>: the proxy's issuer certificate, or <see cref="CurlCode.NotBuiltIn"/> where <see cref="IssuerCertificateIsBuiltIn"/> is off.</summary>
    private static CurlCode SetProxyIssuerCertificate(ref EasyOptions o, object? v) =>
        IssuerCertificateIsBuiltIn ? Proxy.SetIssuerCertificate(ref o, v) : CurlCode.NotBuiltIn;

    /// <summary><c>CURLOPT_ISSUERCERT_BLOB</c>: the origin's issuer certificate bytes, or <see cref="CurlCode.NotBuiltIn"/> where <see cref="IssuerCertificateIsBuiltIn"/> is off.</summary>
    private static CurlCode SetOriginIssuerCertificateBlob(ref EasyOptions o, object? v) =>
        IssuerCertificateIsBuiltIn ? Origin.SetIssuerCertificateBlob(ref o, v) : CurlCode.NotBuiltIn;

    /// <summary><c>CURLOPT_PROXY_ISSUERCERT_BLOB</c>: the proxy's issuer certificate bytes, or <see cref="CurlCode.NotBuiltIn"/> where <see cref="IssuerCertificateIsBuiltIn"/> is off.</summary>
    private static CurlCode SetProxyIssuerCertificateBlob(ref EasyOptions o, object? v) =>
        IssuerCertificateIsBuiltIn ? Proxy.SetIssuerCertificateBlob(ref o, v) : CurlCode.NotBuiltIn;

    /// <summary>
    /// <c>CURLOPT_SSLENGINE</c>: <see langword="null"/> or empty clears it; a name is <see cref="CurlCode.SslEngineNotFound"/>
    /// where <see cref="OpenSslEngineIsBuiltIn"/> is on, Curl having no engine or provider to load, and
    /// <see cref="CurlCode.NotBuiltIn"/> where it is off, as Schannel's absent <c>set_engine</c> answers.
    /// </summary>
    private static CurlCode SetEngine(ref EasyOptions o, object? v)
    {
        if (string.IsNullOrEmpty((string?)v))
        {
            return Tls(ref o, t => t with { Engine = null });
        }

        return OpenSslEngineIsBuiltIn ? CurlCode.SslEngineNotFound : CurlCode.NotBuiltIn;
    }

    /// <summary><c>CURLOPT_SSLENGINE_DEFAULT</c>: <see cref="CurlCode.Ok"/> with no engine set where <see cref="OpenSslEngineIsBuiltIn"/> is on, else <see cref="CurlCode.NotBuiltIn"/>.</summary>
    private static CurlCode SetEngineDefault(ref EasyOptions o, object? v) => OpenSslEngineIsBuiltIn ? CurlCode.Ok : CurlCode.NotBuiltIn;

    /// <summary><c>CURLOPT_SSL_VERIFYSTATUS</c>: on for non-zero, or <see cref="CurlCode.NotBuiltIn"/> where <see cref="CertificateStatusIsBuiltIn"/> is off.</summary>
    private static CurlCode SetVerifyStatus(ref EasyOptions o, object? v) =>
        CertificateStatusIsBuiltIn ? Tls(ref o, t => t with { VerifyStatus = IsOn(v) }) : CurlCode.NotBuiltIn;

    /// <summary><c>CURLOPT_CA_CACHE_TIMEOUT</c>: below -1 is <see cref="CurlCode.BadFunctionArgument"/>, -1 is forever, and above <see cref="int.MaxValue"/> becomes it.</summary>
    private static CurlCode SetCertificateAuthorityCacheTimeout(ref EasyOptions o, object? v)
    {
        var seconds = NumberOf(v);
        if (seconds < -1)
        {
            return CurlCode.BadFunctionArgument;
        }

        var timeout = seconds == -1 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(Math.Min(seconds, LargestCacheTimeoutSeconds));
        return Tls(ref o, t => t with { CertificateAuthorityCacheTimeout = timeout });
    }

    private static CurlCode SetGatherCertificateInfo(ref EasyOptions o, object? v) => Tls(ref o, t => t with { GatherCertificateInfo = IsOn(v) });

    /// <summary><c>CURLOPT_SSL_EC_CURVES</c>: the curves, or <see cref="CurlCode.NotBuiltIn"/> where <see cref="EllipticCurvesIsBuiltIn"/> is off.</summary>
    private static CurlCode SetEllipticCurves(ref EasyOptions o, object? v) =>
        EllipticCurvesIsBuiltIn ? Tls(ref o, t => t with { EllipticCurves = (string?)v }) : CurlCode.NotBuiltIn;

    /// <summary><c>CURLOPT_SSL_SIGNATURE_ALGORITHMS</c>: the algorithms, or <see cref="CurlCode.NotBuiltIn"/> where <see cref="SignatureAlgorithmsIsBuiltIn"/> is off.</summary>
    private static CurlCode SetSignatureAlgorithms(ref EasyOptions o, object? v) =>
        SignatureAlgorithmsIsBuiltIn ? Tls(ref o, t => t with { SignatureAlgorithms = (string?)v }) : CurlCode.NotBuiltIn;

    private static CurlCode SetSessionIdCache(ref EasyOptions o, object? v) => Tls(ref o, t => t with { SessionIdCache = IsOn(v) });

    /// <summary><c>CURLOPT_SSL_FALSESTART</c>: <see cref="CurlCode.NotBuiltIn"/>, as curl 8.22.0 answers, no TLS backend having False Start any more.</summary>
    private static CurlCode SetFalseStart(ref EasyOptions o, object? v) => CurlCode.NotBuiltIn;

    /// <summary>
    /// <c>CURLOPT_ECH</c>: <see langword="null"/> turns ECH off; <c>false</c>, <c>grease</c>, <c>true</c> and
    /// <c>hard</c> set the mode; <c>ecl:</c> and <c>pn:</c> with something after them set the config list and the
    /// public name beside the mode (ADR-0514); anything else changes nothing, as libcurl does. Every value is
    /// <see cref="CurlCode.NotBuiltIn"/> where <see cref="EncryptedClientHelloIsBuiltIn"/> is off.
    /// </summary>
    private static CurlCode SetEncryptedClientHello(ref EasyOptions o, object? v)
    {
        if (!EncryptedClientHelloIsBuiltIn)
        {
            return CurlCode.NotBuiltIn;
        }

        var text = (string?)v;
        return Tls(ref o, t => EncryptedClientHelloOf(t, text));
    }

    /// <summary>The TLS options after <c>CURLOPT_ECH</c> is given <paramref name="text"/>; <paramref name="tls"/> itself when the text matches no form.</summary>
    private static EasyTlsOptions EncryptedClientHelloOf(EasyTlsOptions tls, string? text)
    {
        if (text is null)
        {
            return tls with { EncryptedClientHello = null };
        }

        if (EncryptedClientHelloModes.Contains(text))
        {
            return tls with { EncryptedClientHello = text };
        }

        if (ValueAfter(text, "ecl:") is { } configList)
        {
            return tls with { EncryptedClientHelloConfigList = configList };
        }

        return ValueAfter(text, "pn:") is { } publicName ? tls with { EncryptedClientHelloPublicName = publicName } : tls;
    }

    /// <summary>What follows <paramref name="prefix"/> in <paramref name="text"/>, when at least two characters do, as libcurl's <c>plen &gt;</c> checks require; else <see langword="null"/>.</summary>
    private static string? ValueAfter(string text, string prefix) =>
        text.Length > prefix.Length + 1 && text.StartsWith(prefix, StringComparison.Ordinal) ? text[prefix.Length..] : null;

    private static CurlCode SetDohVerifyPeer(ref EasyOptions o, object? v) => Tls(ref o, t => t with { DohVerifyPeer = IsOn(v) });

    /// <summary><c>CURLOPT_DOH_SSL_VERIFYHOST</c>: on when either of the two low bits is set, as for <c>CURLOPT_SSL_VERIFYHOST</c>.</summary>
    private static CurlCode SetDohVerifyHost(ref EasyOptions o, object? v) => Tls(ref o, t => t with { DohVerifyHost = IsVerifyHostOn(v) });

    /// <summary><c>CURLOPT_DOH_SSL_VERIFYSTATUS</c>: on for non-zero, or <see cref="CurlCode.NotBuiltIn"/> where <see cref="CertificateStatusIsBuiltIn"/> is off.</summary>
    private static CurlCode SetDohVerifyStatus(ref EasyOptions o, object? v) =>
        CertificateStatusIsBuiltIn ? Tls(ref o, t => t with { DohVerifyStatus = IsOn(v) }) : CurlCode.NotBuiltIn;

    /// <summary>
    /// <c>CURLOPT_SSL_ENABLE_NPN</c>, <c>CURLOPT_EGDSOCKET</c> and <c>CURLOPT_RANDOM_FILE</c>: deprecated, and ignored
    /// with <see cref="CurlCode.Ok"/> for every value, as libcurl 8.22.0's <c>setopt.c</c> does (CGR-125).
    /// </summary>
    private static CurlCode IgnoreDeprecated(ref EasyOptions o, object? v) => CurlCode.Ok;

    /// <summary>Replaces the TLS options with what <paramref name="change"/> returns for them; always <see cref="CurlCode.Ok"/>.</summary>
    private static CurlCode Tls(ref EasyOptions options, Func<EasyTlsOptions, EasyTlsOptions> change)
    {
        options = options with { Tls = change(options.Tls) };
        return CurlCode.Ok;
    }

    /// <summary>Whether the option's integer value is not 0, as libcurl treats an on/off option.</summary>
    private static bool IsOn(object? value) => NumberOf(value) != 0;
}
