using System.Collections.Frozen;
using System.Security.Authentication;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// The <c>curl_easy_setopt</c> lines curl 8.21.0 (Schannel) writes for the proxy, TLS and authentication
/// options, measured 2026-10-01 (BL-654 Notes, ADR-0326).
/// </summary>
public static partial class LibcurlSourceCode
{
    private const string PkcsElevenPrefix = "pkcs11:";

    /// <summary>libcurl's <c>CURLAUTH_*</c> names in the order curl's tool tries them when writing a bitmask.</summary>
    private static readonly (string Name, uint Value)[] AuthNames =
    [
        ("CURLAUTH_ANY", AuthAny),
        ("CURLAUTH_ANYSAFE", ~17u),
        ("CURLAUTH_BASIC", 1),
        ("CURLAUTH_DIGEST", 2),
        ("CURLAUTH_GSSNEGOTIATE", 4),
        ("CURLAUTH_NTLM", 8),
        ("CURLAUTH_DIGEST_IE", 16),
        ("CURLAUTH_ONLY", 1u << 31),
        ("CURLAUTH_NONE", 0),
    ];

    /// <summary>libcurl's <c>CURLSSLOPT_*</c> names in the order curl's tool tries them when writing a bitmask.</summary>
    private static readonly (string Name, uint Value)[] SslOptionNames =
    [
        ("CURLSSLOPT_ALLOW_BEAST", 1),
        ("CURLSSLOPT_NO_REVOKE", 2),
        ("CURLSSLOPT_NO_PARTIALCHAIN", 4),
        ("CURLSSLOPT_REVOKE_BEST_EFFORT", 8),
        ("CURLSSLOPT_NATIVE_CA", 16),
        ("CURLSSLOPT_AUTO_CLIENT_CERT", 32),
    ];

    private const uint SslOptionEarlyData = 64;
    private const uint AuthBearer = 64;
    private const uint AuthAwsSigV4 = 128;
    private const uint AuthAny = ~16u;
    private const uint AuthNamedSchemes = 1 | 2 | 4 | 8;

    private static readonly FrozenDictionary<HttpAuthSchemes, uint> CurlAuthBits = new Dictionary<HttpAuthSchemes, uint>
    {
        [HttpAuthSchemes.Basic] = 1,
        [HttpAuthSchemes.Digest] = 2,
        [HttpAuthSchemes.Negotiate] = 4,
        [HttpAuthSchemes.Ntlm] = 8,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<HttpAuthSchemes, string> ProxyAuthNames = new Dictionary<HttpAuthSchemes, string>
    {
        [HttpAuthSchemes.Any] = "CURLAUTH_ANY",
        [HttpAuthSchemes.Basic] = "CURLAUTH_BASIC",
        [HttpAuthSchemes.Digest] = "CURLAUTH_DIGEST",
        [HttpAuthSchemes.Ntlm] = "CURLAUTH_NTLM",
        [HttpAuthSchemes.Negotiate] = "CURLAUTH_GSSNEGOTIATE",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<ProxyKind, string> ProxyTypeNames = new Dictionary<ProxyKind, string>
    {
        [ProxyKind.Http10] = "CURLPROXY_HTTP_1_0",
        [ProxyKind.Https] = "CURLPROXY_HTTPS",
        [ProxyKind.Socks4] = "CURLPROXY_SOCKS4",
        [ProxyKind.Socks4a] = "CURLPROXY_SOCKS4A",
        [ProxyKind.Socks5] = "CURLPROXY_SOCKS5",
        [ProxyKind.Socks5Hostname] = "CURLPROXY_SOCKS5_HOSTNAME",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<SslProtocols, string> TlsVersionNames = new Dictionary<SslProtocols, string>
    {
        [ObsoleteTlsProtocols.Tls10] = "TLSv1_0",
        [ObsoleteTlsProtocols.Tls11] = "TLSv1_1",
        [SslProtocols.Tls12] = "TLSv1_2",
        [SslProtocols.Tls13] = "TLSv1_3",
    }.ToFrozenDictionary();

    /// <summary>
    /// The lines after <c>CURLOPT_NOBODY</c> and before <c>CURLOPT_FAILONERROR</c>: the <c>--oauth2-bearer</c>
    /// token and the proxy options.
    /// </summary>
    private static List<string> BearerAndProxyLines(CommandLineOptions options)
    {
        List<string> lines = [];
        AddIf(lines, options.BearerToken is not null, () => Setopt("CURLOPT_XOAUTH2_BEARER", QuoteCString(options.BearerToken!)));
        AddIf(lines, options.Proxy is not null, () => Setopt("CURLOPT_PROXY", QuoteCString(options.Proxy!.Address)));
        AddIf(lines, options.Proxy is not null && ProxyTypeNames.ContainsKey(options.Proxy.KindWithoutScheme), () => Setopt("CURLOPT_PROXYTYPE", $"(long){ProxyTypeNames[options.Proxy!.KindWithoutScheme]}"));
        AddIf(lines, options.ProxyCredentials is not null, () => Setopt("CURLOPT_PROXYUSERPWD", QuoteCString($"{options.ProxyCredentials!.UserName}:{options.ProxyCredentials.Password}")));
        AddIf(lines, options.ProxyTunnel, SetoptOn("CURLOPT_HTTPPROXYTUNNEL"));
        AddStringIf(lines, "CURLOPT_PRE_PROXY", options.PreProxy);
        AddIf(lines, options.ProxyAuthSchemeRequested, () => Setopt("CURLOPT_PROXYAUTH", $"(long){ProxyAuthNames[options.ProxyAuthSchemes]}"));
        AddStringIf(lines, "CURLOPT_NOPROXY", options.NoProxy);
        AddIf(lines, options.SuppressConnectHeaders, SetoptOn("CURLOPT_SUPPRESS_CONNECT_HEADERS"));
        AddStringIf(lines, "CURLOPT_PROXY_SERVICE_NAME", options.ProxyServiceName);
        AddIf(lines, options.HaproxyProtocol, SetoptOn("CURLOPT_HAPROXYPROTOCOL"));
        AddStringIf(lines, "CURLOPT_HAPROXY_CLIENT_IP", options.HaproxyClientIp);
        return lines;
    }

    /// <summary>The netrc and <c>--login-options</c> lines, after <c>CURLOPT_FAILONERROR</c> and before <c>CURLOPT_USERPWD</c>.</summary>
    private static List<string> NetrcLines(CommandLineOptions options)
    {
        List<string> lines = [];
        AddIf(lines, options.NetrcUse != NetrcUse.Ignored, () => Setopt("CURLOPT_NETRC", options.NetrcUse == NetrcUse.Optional ? "(long)CURL_NETRC_OPTIONAL" : "(long)CURL_NETRC_REQUIRED"));
        AddStringIf(lines, "CURLOPT_NETRC_FILE", options.NetrcFile);
        AddStringIf(lines, "CURLOPT_LOGIN_OPTIONS", options.LoginOptions);
        return lines;
    }

    /// <summary>
    /// The <c>CURLOPT_HTTPAUTH</c> line, written after the request body whenever an authentication option
    /// gave curl's tool a scheme: <c>--anyauth</c> starts from <c>CURLAUTH_ANY</c> and a later <c>--no-</c>
    /// scheme takes its bit out, <c>--oauth2-bearer</c> adds Bearer (64) and <c>--aws-sigv4</c> AWS (128).
    /// </summary>
    private static List<string> HttpAuthLines(CommandLineOptions options)
    {
        uint requested = CurlAuthBits.Where(pair => (options.RequestedAuthSchemes & pair.Key) != 0).Aggregate(0u, (all, pair) => all | pair.Value);
        uint bits = options.EveryAuthSchemeRequested ? AuthAny & ~(AuthNamedSchemes & ~requested) : requested;
        bits |= options.BearerToken is null ? 0 : AuthBearer;
        bits |= options.AwsSigV4 is null ? 0 : AuthAwsSigV4;
        return bits == 0 ? [] : [SetoptBitmask("CURLOPT_HTTPAUTH", bits, AuthNames)];
    }

    /// <summary>The TLS lines for the server and the proxy, written after the scheme's lines in curl's order.</summary>
    private static List<string> TlsLines(CommandLineOptions options)
    {
        (string? certificate, string? certificatePassword) = SplitCertificate(options.ClientCertificate);
        (string? proxyCertificate, string? proxyCertificatePassword) = SplitCertificate(options.ProxyClientCertificate);
        List<string> lines = [];
        AddStringIf(lines, "CURLOPT_KEYPASSWD", options.Passphrase ?? certificatePassword);
        AddStringIf(lines, "CURLOPT_PROXY_KEYPASSWD", options.ProxyPassphrase ?? proxyCertificatePassword);
        AddStringIf(lines, "CURLOPT_CAINFO", options.CaCertificateFile);
        AddStringIf(lines, "CURLOPT_PROXY_CAINFO", options.ProxyCaCertificateFile);
        AddStringIf(lines, "CURLOPT_PINNEDPUBLICKEY", options.PinnedPublicKey);
        AddStringIf(lines, "CURLOPT_PROXY_PINNEDPUBLICKEY", options.ProxyPinnedPublicKey);
        lines.AddRange(CertificateAndKeyLines(options, certificate, proxyCertificate));
        lines.AddRange(VerificationAndVersionLines(options));
        lines.AddRange(SslOptionAndCipherLines(options));
        return lines;
    }

    private static List<string> CertificateAndKeyLines(CommandLineOptions options, string? certificate, string? proxyCertificate)
    {
        List<string> lines = [];
        AddStringIf(lines, "CURLOPT_SSLCERT", certificate);
        AddStringIf(lines, "CURLOPT_PROXY_SSLCERT", proxyCertificate);
        AddStringIf(lines, "CURLOPT_SSLCERTTYPE", options.ClientCertificateType ?? EngineTypeFor(certificate));
        AddStringIf(lines, "CURLOPT_PROXY_SSLCERTTYPE", options.ProxyClientCertificateType ?? EngineTypeFor(proxyCertificate));
        AddStringIf(lines, "CURLOPT_SSLKEY", options.PrivateKey);
        AddStringIf(lines, "CURLOPT_PROXY_SSLKEY", options.ProxyPrivateKey);
        AddStringIf(lines, "CURLOPT_SSLKEYTYPE", options.PrivateKeyType ?? EngineTypeFor(options.PrivateKey));
        AddStringIf(lines, "CURLOPT_PROXY_SSLKEYTYPE", options.ProxyPrivateKeyType ?? EngineTypeFor(options.ProxyPrivateKey));
        return lines;
    }

    private static List<string> VerificationAndVersionLines(CommandLineOptions options)
    {
        List<string> lines = [];
        AddIf(lines, options.Insecure, () => Setopt("CURLOPT_SSL_VERIFYPEER", "0L"));
        AddIf(lines, options.Insecure, () => Setopt("CURLOPT_SSL_VERIFYHOST", "0L"));
        AddIf(lines, options.ProxyInsecure, () => Setopt("CURLOPT_PROXY_SSL_VERIFYPEER", "0L"));
        AddIf(lines, options.ProxyInsecure, () => Setopt("CURLOPT_PROXY_SSL_VERIFYHOST", "0L"));
        lines.AddRange(VersionLines(options));
        return lines;
    }

    private static List<string> VersionLines(CommandLineOptions options)
    {
        List<string> lines = [];
        lines.Add(Setopt("CURLOPT_SSLVERSION", TlsVersionValue(options.MinimumTlsVersion ?? SslProtocols.Tls12, options.MaximumTlsVersion)));
        AddIf(lines, options.Proxy is not null && options.ProxyMinimumTlsVersion is not null, () => Setopt("CURLOPT_PROXY_SSLVERSION", "(long)CURL_SSLVERSION_TLSv1"));
        return lines;
    }

    private static List<string> SslOptionAndCipherLines(CommandLineOptions options)
    {
        List<string> lines = [];
        uint sslOptions = Bits(options.AllowBeast, 1) | Bits(options.SkipRevocationCheck, 2) | Bits(options.RevocationCheckBestEffort, 8)
            | Bits(options.UseNativeCaStore, 16) | Bits(options.AutoClientCertificate, 32) | Bits(options.TlsEarlyData, SslOptionEarlyData);
        AddIf(lines, sslOptions != 0, () => SetoptBitmask("CURLOPT_SSL_OPTIONS", sslOptions, SslOptionNames));
        uint proxySslOptions = Bits(options.ProxyAllowBeast, 1) | Bits(options.ProxyUseNativeCaStore, 16) | Bits(options.ProxyAutoClientCertificate, 32);
        AddIf(lines, proxySslOptions != 0, () => SetoptBitmask("CURLOPT_PROXY_SSL_OPTIONS", proxySslOptions, SslOptionNames));
        AddStringIf(lines, "CURLOPT_SSL_CIPHER_LIST", options.Ciphers);
        AddStringIf(lines, "CURLOPT_PROXY_SSL_CIPHER_LIST", options.ProxyCiphers);
        AddIf(lines, !options.UseAlpn, () => Setopt("CURLOPT_SSL_ENABLE_ALPN", "0L"));
        return lines;
    }

    /// <summary>The SOCKS5 and <c>--service-name</c> lines, after <c>CURLOPT_IPRESOLVE</c> and before <c>CURLOPT_TCP_KEEPALIVE</c>.</summary>
    private static List<string> SocksAndServiceLines(CommandLineOptions options)
    {
        List<string> lines = [];
        AddIf(lines, options.Socks5GssapiNec, SetoptOn("CURLOPT_SOCKS5_GSSAPI_NEC"));
        uint socksAuth = Bits(options.Socks5BasicAuth, 1) | Bits(options.Socks5GssapiAuth, 4);
        AddIf(lines, socksAuth != 0, () => SetoptBitmask("CURLOPT_SOCKS5_AUTH", socksAuth, AuthNames));
        AddStringIf(lines, "CURLOPT_SERVICE_NAME", options.ServiceName);
        return lines;
    }

    /// <summary>The GSS-API delegation and SASL lines, the last of a transfer's lines.</summary>
    private static List<string> DelegationAndSaslLines(CommandLineOptions options)
    {
        List<string> lines = [];
        AddIf(lines, options.GssApiDelegation != GssApiDelegation.None, () => Setopt("CURLOPT_GSSAPI_DELEGATION", $"{(int)options.GssApiDelegation}L"));
        AddStringIf(lines, "CURLOPT_SASL_AUTHZID", options.SaslAuthorizationIdentity);
        AddIf(lines, options.SaslInitialResponse, SetoptOn("CURLOPT_SASL_IR"));
        return lines;
    }

    /// <summary>
    /// <see langword="true"/> when curl 8.21.0 writes <c>CURLOPT_HEADEROPT</c> for an HTTP transfer: through a
    /// proxy, to an <c>https</c> URL or through a <c>-p</c> tunnel.
    /// </summary>
    private static bool SeparatesProxyHeaders(CommandLineOptions options, string scheme) =>
        options.Proxy is not null && (scheme == "https" || options.ProxyTunnel);

    private static uint Bits(bool on, uint bits) => on ? bits : 0;

    private static void AddStringIf(List<string> lines, string option, string? value) =>
        AddIf(lines, value is not null, () => Setopt(option, QuoteCString(value!)));

    private static string TlsVersionValue(SslProtocols minimum, SslProtocols? maximum) =>
        maximum is { } highest
            ? $"(long)(CURL_SSLVERSION_{TlsVersionNames[minimum]} | CURL_SSLVERSION_MAX_{TlsVersionNames[highest]})"
            : $"(long)CURL_SSLVERSION_{TlsVersionNames[minimum]}";

    /// <summary>
    /// Writes a bitmask the way curl's tool does: each name whose bits are all still unwritten, in the order
    /// given, as <c>(long)NAME</c>; then any bits no name covers as <c>&lt;n&gt;UL</c>; joined by <c> |</c> and a
    /// new line indented to the value's column.
    /// </summary>
    private static string SetoptBitmask(string option, uint value, (string Name, uint Value)[] names)
    {
        string preamble = $"  curl_easy_setopt(curl, {option}, ";
        List<string> pieces = [];
        uint rest = value;
        foreach ((string name, uint bits) in names)
        {
            if (rest != 0 && (bits & ~rest) == 0)
            {
                rest &= ~bits;
                pieces.Add($"(long){name}");
            }
        }

        AddIf(pieces, rest != 0, () => $"{rest}UL");
        return preamble + string.Join(" |\n" + new string(' ', preamble.Length), pieces) + ");";
    }

    /// <summary>
    /// <c>ENG</c> for a <c>pkcs11:</c> certificate or key with no type given, as curl's tool sets it; otherwise
    /// <see langword="null"/>.
    /// </summary>
    private static string? EngineTypeFor(string? file) =>
        file is not null && file.StartsWith(PkcsElevenPrefix, StringComparison.OrdinalIgnoreCase) ? "ENG" : null;

    /// <summary>
    /// Splits <c>-E</c>'s <c>certificate[:password]</c> as curl 8.21.0's Windows tool does: a <c>pkcs11:</c>
    /// value is not split; <c>\:</c> is a colon and <c>\\</c> a backslash; a colon after a drive letter and
    /// before <c>\</c> or <c>/</c> is part of the name; the first other colon ends the name, and an empty
    /// password is no password.
    /// </summary>
    private static (string? Certificate, string? Password) SplitCertificate(string? value)
    {
        if (value is null || value.StartsWith(PkcsElevenPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (value, null);
        }

        return SplitAtPasswordColon(value);
    }

    private static (string? Certificate, string? Password) SplitAtPasswordColon(string value)
    {
        StringBuilder name = new();
        for (int index = 0; index < value.Length; index++)
        {
            if (IsEscapedCertificateCharacter(value, index))
            {
                name.Append(value[++index]);
            }
            else if (value[index] == ':' && !IsDriveColon(value, index))
            {
                string password = value[(index + 1)..];
                return (name.ToString(), password.Length == 0 ? null : password);
            }
            else
            {
                name.Append(value[index]);
            }
        }

        return (name.ToString(), null);
    }

    private static bool IsEscapedCertificateCharacter(string value, int index) =>
        value[index] == '\\' && index + 1 < value.Length && value[index + 1] is ':' or '\\';

    private static bool IsDriveColon(string value, int index) =>
        index == 1 && char.IsAsciiLetter(value[0]) && value.Length > 2 && value[2] is '\\' or '/';
}
