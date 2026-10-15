using System.Text;

namespace Curl.Api.Options.Authentication;

/// <summary>
/// An easy handle's authentication options (ADR-0506, plan row
/// <c>EasyAuthenticationOptions</c>): <c>.netrc</c>, the origin's and the proxy's credentials
/// and methods, SASL, the OAuth 2.0 bearer token, GSS-API service names and delegation, the
/// Kerberos level for FTP, AWS SigV4, TLS-SRP for the origin and the proxy, and Curl's HTTP
/// message signature settings, which have no <c>CURLOPT_*</c>. Every libcurl default is libcurl
/// 8.22.0's. <see cref="ToString"/> never prints a password, a combined user-and-password
/// string, a bearer token or a signature key: it prints <c>&lt;redacted&gt;</c> for one that
/// is set.
/// </summary>
public sealed record EasyAuthenticationOptions
{
    private const string RedactedSecret = "<redacted>";

    /// <summary>
    /// <c>CURLOPT_NETRC</c>: whether the <c>.netrc</c> file supplies credentials. Default
    /// <see cref="NetrcUsage.Ignored"/> (<c>CURL_NETRC_IGNORED</c>).
    /// </summary>
    public NetrcUsage Netrc { get; init; } = NetrcUsage.Ignored;

    /// <summary>
    /// <c>CURLOPT_NETRC_FILE</c>: the <c>.netrc</c> file to read. Default
    /// <see langword="null"/>: <c>.netrc</c> (or <c>_netrc</c>) in the home directory.
    /// </summary>
    public string? NetrcFile { get; init; }

    /// <summary>
    /// <c>CURLOPT_USERPWD</c>: <c>user[:password]</c> for the origin, a secret. Default
    /// <see langword="null"/>: no credentials beyond the URL's.
    /// </summary>
    public string? UserPassword { get; init; }

    /// <summary>
    /// <c>CURLOPT_USERNAME</c>: the origin's user name, taking precedence over
    /// <see cref="UserPassword"/>. Default <see langword="null"/>.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// <c>CURLOPT_PASSWORD</c>: the origin's password, a secret. Default
    /// <see langword="null"/>.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// <c>CURLOPT_LOGIN_OPTIONS</c>: protocol login options such as IMAP's
    /// <c>AUTH=NTLM</c>. Default <see langword="null"/>.
    /// </summary>
    public string? LoginOptions { get; init; }

    /// <summary>
    /// <c>CURLOPT_HTTPAUTH</c>: the methods the origin may be authenticated with. Default
    /// <see cref="HttpAuthenticationMethods.Basic"/> (<c>CURLAUTH_BASIC</c>).
    /// </summary>
    public HttpAuthenticationMethods HttpAuthentication { get; init; } = HttpAuthenticationMethods.Basic;

    /// <summary>
    /// <c>CURLOPT_PROXYAUTH</c>: the methods the proxy may be authenticated with. Default
    /// <see cref="HttpAuthenticationMethods.Basic"/> (<c>CURLAUTH_BASIC</c>).
    /// </summary>
    public HttpAuthenticationMethods ProxyAuthentication { get; init; } = HttpAuthenticationMethods.Basic;

    /// <summary>
    /// <c>CURLOPT_PROXYUSERPWD</c>: <c>user[:password]</c> for the proxy, a secret. Default
    /// <see langword="null"/>: no proxy credentials beyond the proxy URL's.
    /// </summary>
    public string? ProxyUserPassword { get; init; }

    /// <summary>
    /// <c>CURLOPT_PROXYUSERNAME</c>: the proxy's user name, taking precedence over
    /// <see cref="ProxyUserPassword"/>. Default <see langword="null"/>.
    /// </summary>
    public string? ProxyUserName { get; init; }

    /// <summary>
    /// <c>CURLOPT_PROXYPASSWORD</c>: the proxy's password, a secret. Default
    /// <see langword="null"/>.
    /// </summary>
    public string? ProxyPassword { get; init; }

    /// <summary>
    /// <c>CURLOPT_SASL_AUTHZID</c>: the SASL authorization identity, the user to act as.
    /// Default <see langword="null"/>: the authentication identity.
    /// </summary>
    public string? SaslAuthorizationIdentity { get; init; }

    /// <summary>
    /// <c>CURLOPT_SASL_IR</c>: whether the SASL initial response is sent with the
    /// <c>AUTH</c> command. Default <see langword="false"/> (0).
    /// </summary>
    public bool SaslInitialResponse { get; init; }

    /// <summary>
    /// <c>CURLOPT_XOAUTH2_BEARER</c>: the OAuth 2.0 bearer token, a secret. Default
    /// <see langword="null"/>.
    /// </summary>
    public string? XOAuth2Bearer { get; init; }

    /// <summary>
    /// <c>CURLOPT_DISALLOW_USERNAME_IN_URL</c>: whether a URL carrying a user name is
    /// refused. Default <see langword="false"/> (0).
    /// </summary>
    public bool DisallowUserNameInUrl { get; init; }

    /// <summary>
    /// <c>CURLOPT_UNRESTRICTED_AUTH</c>: whether credentials follow a redirect to another
    /// host. Default <see langword="false"/> (0).
    /// </summary>
    public bool UnrestrictedAuthentication { get; init; }

    /// <summary>
    /// <c>CURLOPT_SERVICE_NAME</c>: the origin's service name for Negotiate, Kerberos and
    /// SASL. Default <see langword="null"/>: <c>HTTP</c> for HTTP, the protocol's own name
    /// otherwise.
    /// </summary>
    public string? ServiceName { get; init; }

    /// <summary>
    /// <c>CURLOPT_PROXY_SERVICE_NAME</c>: the proxy's service name for Negotiate and SOCKS5
    /// GSS-API. Default <see langword="null"/>: <c>HTTP</c> for an HTTP proxy, <c>rcmd</c>
    /// for SOCKS5.
    /// </summary>
    public string? ProxyServiceName { get; init; }

    /// <summary>
    /// <c>CURLOPT_GSSAPI_DELEGATION</c>: whether the GSS-API server may act on the user's
    /// behalf. Default <see cref="GssApiDelegation.None"/> (<c>CURLGSSAPI_DELEGATION_NONE</c>).
    /// </summary>
    public GssApiDelegation GssApiDelegation { get; init; } = GssApiDelegation.None;

    /// <summary>
    /// <c>CURLOPT_KRBLEVEL</c>: FTP's Kerberos security level (<c>clear</c>, <c>safe</c>,
    /// <c>confidential</c>, <c>private</c>). Default <see langword="null"/>: Kerberos is off.
    /// </summary>
    public string? KerberosLevel { get; init; }

    /// <summary>
    /// <c>CURLOPT_AWS_SIGV4</c>: <c>provider1[:provider2[:region[:service]]]</c> for AWS
    /// Signature Version 4. Default <see langword="null"/>: no signing.
    /// </summary>
    public string? AwsSigV4 { get; init; }

    /// <summary>
    /// <c>CURLOPT_TLSAUTH_TYPE</c>: the origin's TLS authentication method; only <c>SRP</c>
    /// exists. Default <see langword="null"/>: none (<c>SRP</c> once a user name is set).
    /// </summary>
    public string? TlsAuthType { get; init; }

    /// <summary>
    /// <c>CURLOPT_TLSAUTH_USERNAME</c>: the origin's TLS-SRP user name. Default
    /// <see langword="null"/>.
    /// </summary>
    public string? TlsAuthUserName { get; init; }

    /// <summary>
    /// <c>CURLOPT_TLSAUTH_PASSWORD</c>: the origin's TLS-SRP password, a secret. Default
    /// <see langword="null"/>.
    /// </summary>
    public string? TlsAuthPassword { get; init; }

    /// <summary>
    /// <c>CURLOPT_PROXY_TLSAUTH_TYPE</c>: the HTTPS proxy's TLS authentication method; only
    /// <c>SRP</c> exists. Default <see langword="null"/>: none (<c>SRP</c> once a user name is
    /// set).
    /// </summary>
    public string? ProxyTlsAuthType { get; init; }

    /// <summary>
    /// <c>CURLOPT_PROXY_TLSAUTH_USERNAME</c>: the HTTPS proxy's TLS-SRP user name. Default
    /// <see langword="null"/>.
    /// </summary>
    public string? ProxyTlsAuthUserName { get; init; }

    /// <summary>
    /// <c>CURLOPT_PROXY_TLSAUTH_PASSWORD</c>: the HTTPS proxy's TLS-SRP password, a secret.
    /// Default <see langword="null"/>.
    /// </summary>
    public string? ProxyTlsAuthPassword { get; init; }

    /// <summary>
    /// Curl-specific, with no <c>CURLOPT_*</c>: the HTTP message signature algorithm the curl
    /// tool's <c>--httpsig-algo</c> names (<c>ed25519</c> or <c>hmac-sha256</c>), verbatim and
    /// unchecked. Default <see langword="null"/>: requests are not signed.
    /// </summary>
    public string? HttpSignatureAlgorithm { get; init; }

    /// <summary>
    /// Curl-specific, with no <c>CURLOPT_*</c>: the HTTP message signature key the curl tool's
    /// <c>--httpsig-key</c> names, a secret: the hex key, or <c>@</c> and the name of a file
    /// holding it. Default <see langword="null"/>.
    /// </summary>
    public string? HttpSignatureKey { get; init; }

    /// <summary>
    /// Curl-specific, with no <c>CURLOPT_*</c>: the key identifier the curl tool's
    /// <c>--httpsig-keyid</c> names. Default <see langword="null"/>.
    /// </summary>
    public string? HttpSignatureKeyId { get; init; }

    /// <summary>
    /// Curl-specific, with no <c>CURLOPT_*</c>: the components to sign the curl tool's
    /// <c>--httpsig-headers</c> names, verbatim and unparsed. Default <see langword="null"/>:
    /// curl's default set.
    /// </summary>
    public string? HttpSignatureHeaders { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Netrc = {Netrc}, NetrcFile = {NetrcFile}, ");
        builder.Append($"UserPassword = {Redact(UserPassword)}, UserName = {UserName}, ");
        builder.Append($"Password = {Redact(Password)}, LoginOptions = {LoginOptions}, ");
        builder.Append($"HttpAuthentication = {HttpAuthentication}, ProxyAuthentication = {ProxyAuthentication}, ");
        builder.Append($"ProxyUserPassword = {Redact(ProxyUserPassword)}, ProxyUserName = {ProxyUserName}, ");
        builder.Append($"ProxyPassword = {Redact(ProxyPassword)}, ");
        builder.Append($"SaslAuthorizationIdentity = {SaslAuthorizationIdentity}, SaslInitialResponse = {SaslInitialResponse}, ");
        builder.Append($"XOAuth2Bearer = {Redact(XOAuth2Bearer)}, DisallowUserNameInUrl = {DisallowUserNameInUrl}, ");
        builder.Append($"UnrestrictedAuthentication = {UnrestrictedAuthentication}, ");
        builder.Append($"ServiceName = {ServiceName}, ProxyServiceName = {ProxyServiceName}, ");
        builder.Append($"GssApiDelegation = {GssApiDelegation}, KerberosLevel = {KerberosLevel}, AwsSigV4 = {AwsSigV4}, ");
        builder.Append($"TlsAuthType = {TlsAuthType}, TlsAuthUserName = {TlsAuthUserName}, ");
        builder.Append($"TlsAuthPassword = {Redact(TlsAuthPassword)}, ");
        builder.Append($"ProxyTlsAuthType = {ProxyTlsAuthType}, ProxyTlsAuthUserName = {ProxyTlsAuthUserName}, ");
        builder.Append($"ProxyTlsAuthPassword = {Redact(ProxyTlsAuthPassword)}, ");
        builder.Append($"HttpSignatureAlgorithm = {HttpSignatureAlgorithm}, HttpSignatureKey = {Redact(HttpSignatureKey)}, ");
        builder.Append($"HttpSignatureKeyId = {HttpSignatureKeyId}, HttpSignatureHeaders = {HttpSignatureHeaders}");
        return true;
    }

    private static string? Redact(string? secret) => secret is null ? null : RedactedSecret;
}
