namespace Libcurl.Misc.Version;

/// <summary>The <c>CURL_VERSION_*</c> feature bits of <c>curl_version_info_data.features</c>, as curl 8.22.0's <c>curl.h</c> numbers them.</summary>
[Flags]
public enum CurlVersionFeatures
{
    /// <summary>No feature bit.</summary>
    None = 0,

    /// <summary><c>CURL_VERSION_IPV6</c>: IPv6-enabled (<c>IPv6</c>).</summary>
    IPv6 = 1 << 0,

    /// <summary><c>CURL_VERSION_KERBEROS4</c>: Kerberos V4 authentication (deprecated; never set).</summary>
    Kerberos4 = 1 << 1,

    /// <summary><c>CURL_VERSION_SSL</c>: TLS options are present (<c>SSL</c>).</summary>
    Ssl = 1 << 2,

    /// <summary><c>CURL_VERSION_LIBZ</c>: gzip and deflate decoding (<c>libz</c>).</summary>
    Libz = 1 << 3,

    /// <summary><c>CURL_VERSION_NTLM</c>: NTLM authentication (<c>NTLM</c>).</summary>
    Ntlm = 1 << 4,

    /// <summary><c>CURL_VERSION_GSSNEGOTIATE</c>: Negotiate authentication (deprecated; never set).</summary>
    GssNegotiate = 1 << 5,

    /// <summary><c>CURL_VERSION_DEBUG</c>: built with debug capabilities (<c>Debug</c>).</summary>
    Debug = 1 << 6,

    /// <summary><c>CURL_VERSION_ASYNCHDNS</c>: asynchronous name resolves (<c>AsynchDNS</c>).</summary>
    AsynchDns = 1 << 7,

    /// <summary><c>CURL_VERSION_SPNEGO</c>: SPNEGO authentication (<c>SPNEGO</c>).</summary>
    Spnego = 1 << 8,

    /// <summary><c>CURL_VERSION_LARGEFILE</c>: files larger than 2 GB (<c>Largefile</c>).</summary>
    LargeFile = 1 << 9,

    /// <summary><c>CURL_VERSION_IDN</c>: internationalized domain names (<c>IDN</c>).</summary>
    Idn = 1 << 10,

    /// <summary><c>CURL_VERSION_SSPI</c>: built against Windows SSPI (<c>SSPI</c>).</summary>
    Sspi = 1 << 11,

    /// <summary><c>CURL_VERSION_CONV</c>: character conversions (deprecated; never set).</summary>
    Conv = 1 << 12,

    /// <summary><c>CURL_VERSION_CURLDEBUG</c>: debug memory tracking (deprecated; set with <c>Debug</c> since 8.19.0).</summary>
    CurlDebug = 1 << 13,

    /// <summary><c>CURL_VERSION_TLSAUTH_SRP</c>: TLS-SRP authentication (<c>TLS-SRP</c>).</summary>
    TlsAuthSrp = 1 << 14,

    /// <summary><c>CURL_VERSION_NTLM_WB</c>: NTLM delegation to winbind (<c>NTLM_WB</c>).</summary>
    NtlmWinbind = 1 << 15,

    /// <summary><c>CURL_VERSION_HTTP2</c>: HTTP/2 (<c>HTTP2</c>).</summary>
    Http2 = 1 << 16,

    /// <summary><c>CURL_VERSION_GSSAPI</c>: built against a GSS-API library (<c>GSS-API</c>).</summary>
    GssApi = 1 << 17,

    /// <summary><c>CURL_VERSION_KERBEROS5</c>: Kerberos V5 authentication (<c>Kerberos</c>).</summary>
    Kerberos5 = 1 << 18,

    /// <summary><c>CURL_VERSION_UNIX_SOCKETS</c>: Unix domain sockets (<c>UnixSockets</c>).</summary>
    UnixSockets = 1 << 19,

    /// <summary><c>CURL_VERSION_PSL</c>: the Public Suffix List for cookie domains (<c>PSL</c>).</summary>
    Psl = 1 << 20,

    /// <summary><c>CURL_VERSION_HTTPS_PROXY</c>: HTTPS proxies (<c>HTTPS-proxy</c>).</summary>
    HttpsProxy = 1 << 21,

    /// <summary><c>CURL_VERSION_MULTI_SSL</c>: several TLS backends (<c>MultiSSL</c>).</summary>
    MultiSsl = 1 << 22,

    /// <summary><c>CURL_VERSION_BROTLI</c>: brotli decoding (<c>brotli</c>).</summary>
    Brotli = 1 << 23,

    /// <summary><c>CURL_VERSION_ALTSVC</c>: Alt-Svc handling (<c>alt-svc</c>).</summary>
    AltSvc = 1 << 24,

    /// <summary><c>CURL_VERSION_HTTP3</c>: HTTP/3 (<c>HTTP3</c>).</summary>
    Http3 = 1 << 25,

    /// <summary><c>CURL_VERSION_ZSTD</c>: zstd decoding (<c>zstd</c>).</summary>
    Zstd = 1 << 26,

    /// <summary><c>CURL_VERSION_UNICODE</c>: Unicode support on Windows (<c>Unicode</c>).</summary>
    Unicode = 1 << 27,

    /// <summary><c>CURL_VERSION_HSTS</c>: HSTS (<c>HSTS</c>).</summary>
    Hsts = 1 << 28,

    /// <summary><c>CURL_VERSION_GSASL</c>: libgsasl (<c>gsasl</c>).</summary>
    Gsasl = 1 << 29,

    /// <summary><c>CURL_VERSION_THREADSAFE</c>: the API is thread-safe (<c>threadsafe</c>).</summary>
    ThreadSafe = 1 << 30,
}
