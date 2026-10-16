namespace Curl.Api.Options.Tls;

/// <summary>
/// The <c>CURLSSLOPT_*</c> bits of <c>CURLOPT_SSL_OPTIONS</c> and
/// <c>CURLOPT_PROXY_SSL_OPTIONS</c>. Each member's value is libcurl's bit.
/// </summary>
[Flags]
public enum SslOptions
{
    /// <summary>No bit set; libcurl's default.</summary>
    None = 0,

    /// <summary><c>CURLSSLOPT_ALLOW_BEAST</c> (1): keep the BEAST workaround off.</summary>
    AllowBeast = 1 << 0,

    /// <summary><c>CURLSSLOPT_NO_REVOKE</c> (2): skip certificate revocation checks (Schannel).</summary>
    NoRevoke = 1 << 1,

    /// <summary><c>CURLSSLOPT_NO_PARTIALCHAIN</c> (4): do not accept a partial chain as trusted.</summary>
    NoPartialChain = 1 << 2,

    /// <summary><c>CURLSSLOPT_REVOKE_BEST_EFFORT</c> (8): ignore revocation checks that cannot be made (Schannel).</summary>
    RevokeBestEffort = 1 << 3,

    /// <summary><c>CURLSSLOPT_NATIVE_CA</c> (16): use the operating system's CA store.</summary>
    NativeCa = 1 << 4,

    /// <summary><c>CURLSSLOPT_AUTO_CLIENT_CERT</c> (32): let the TLS library pick a client certificate (Schannel).</summary>
    AutoClientCert = 1 << 5,

    /// <summary><c>CURLSSLOPT_EARLYDATA</c> (64): send TLS 1.3 early data when a session allows it.</summary>
    EarlyData = 1 << 6,
}
