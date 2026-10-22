using System.Text;
using Curl.Api.Options.Authentication;

namespace Curl.Api;

/// <summary>
/// Authentication settings, mapped one to one onto <see cref="EasyAuthenticationOptions"/> (BL-2503,
/// ADR-0640). Each member starts at libcurl 8.22.0's default, so <c>new CurlAuthenticationOptions()</c>
/// changes nothing. Set on <see cref="CurlClientOptions.Authentication"/> for every request or on
/// <see cref="CurlRequest.Authentication"/> for one; a request's record replaces the client's whole. An
/// invalid value throws as it is set. <see cref="ToString"/> prints <c>&lt;redacted&gt;</c> for every
/// password, combined user-and-password string, bearer token and signature key that is set.
/// </summary>
public sealed record CurlAuthenticationOptions : IEasyOptionRecord<EasyAuthenticationOptions>
{
    /// <summary>What <see cref="ToString"/> prints in place of a secret that is set.</summary>
    private const string RedactedSecret = "<redacted>";

    /// <summary><c>CURLOPT_NETRC</c>: whether <c>.netrc</c> supplies credentials. Default <see cref="NetrcUsage.Ignored"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="NetrcUsage"/> value.</exception>
    public NetrcUsage Netrc
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(Netrc));
    } = NetrcUsage.Ignored;

    /// <summary><c>CURLOPT_NETRC_FILE</c>: the <c>.netrc</c> file. Default <see langword="null"/>: the home directory's.</summary>
    public string? NetrcFile { get; init; }

    /// <summary><c>CURLOPT_USERPWD</c>: <c>user[:password]</c> for the origin, a secret. Default <see langword="null"/>.</summary>
    public string? UserPassword { get; init; }

    /// <summary><c>CURLOPT_USERNAME</c>: the origin's user name. Default <see langword="null"/>.</summary>
    public string? UserName { get; init; }

    /// <summary><c>CURLOPT_PASSWORD</c>: the origin's password, a secret. Default <see langword="null"/>.</summary>
    public string? Password { get; init; }

    /// <summary><c>CURLOPT_LOGIN_OPTIONS</c>: protocol login options such as <c>AUTH=NTLM</c>. Default <see langword="null"/>.</summary>
    public string? LoginOptions { get; init; }

    /// <summary><c>CURLOPT_HTTPAUTH</c>: the methods the origin may be authenticated with. Default <see cref="HttpAuthenticationMethods.Basic"/>.</summary>
    public HttpAuthenticationMethods HttpAuthentication { get; init; } = HttpAuthenticationMethods.Basic;

    /// <summary><c>CURLOPT_PROXYAUTH</c>: the methods the proxy may be authenticated with. Default <see cref="HttpAuthenticationMethods.Basic"/>.</summary>
    public HttpAuthenticationMethods ProxyAuthentication { get; init; } = HttpAuthenticationMethods.Basic;

    /// <summary><c>CURLOPT_PROXYUSERPWD</c>: <c>user[:password]</c> for the proxy, a secret. Default <see langword="null"/>.</summary>
    public string? ProxyUserPassword { get; init; }

    /// <summary><c>CURLOPT_PROXYUSERNAME</c>: the proxy's user name. Default <see langword="null"/>.</summary>
    public string? ProxyUserName { get; init; }

    /// <summary><c>CURLOPT_PROXYPASSWORD</c>: the proxy's password, a secret. Default <see langword="null"/>.</summary>
    public string? ProxyPassword { get; init; }

    /// <summary><c>CURLOPT_SASL_AUTHZID</c>: the SASL authorization identity. Default <see langword="null"/>.</summary>
    public string? SaslAuthorizationIdentity { get; init; }

    /// <summary><c>CURLOPT_SASL_IR</c>: send the SASL initial response with <c>AUTH</c>. Default <see langword="false"/>.</summary>
    public bool SaslInitialResponse { get; init; }

    /// <summary><c>CURLOPT_XOAUTH2_BEARER</c>: the OAuth 2.0 bearer token, a secret. Default <see langword="null"/>.</summary>
    public string? XOAuth2Bearer { get; init; }

    /// <summary><c>CURLOPT_DISALLOW_USERNAME_IN_URL</c>: refuse a URL carrying a user name. Default <see langword="false"/>.</summary>
    public bool DisallowUserNameInUrl { get; init; }

    /// <summary><c>CURLOPT_UNRESTRICTED_AUTH</c>: credentials follow a redirect to another host. Default <see langword="false"/>.</summary>
    public bool UnrestrictedAuthentication { get; init; }

    /// <summary><c>CURLOPT_SERVICE_NAME</c>: the origin's Negotiate, Kerberos and SASL service name. Default <see langword="null"/>.</summary>
    public string? ServiceName { get; init; }

    /// <summary><c>CURLOPT_PROXY_SERVICE_NAME</c>: the proxy's Negotiate and SOCKS5 GSS-API service name. Default <see langword="null"/>.</summary>
    public string? ProxyServiceName { get; init; }

    /// <summary><c>CURLOPT_GSSAPI_DELEGATION</c>: whether the GSS-API server may act for the user. Default <see cref="GssApiDelegation.None"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="Options.Authentication.GssApiDelegation"/> value.</exception>
    public GssApiDelegation GssApiDelegation
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(GssApiDelegation));
    }

    /// <summary><c>CURLOPT_KRBLEVEL</c>: FTP's Kerberos security level. Default <see langword="null"/>: Kerberos is off.</summary>
    public string? KerberosLevel { get; init; }

    /// <summary><c>CURLOPT_AWS_SIGV4</c>: <c>provider1[:provider2[:region[:service]]]</c>. Default <see langword="null"/>: no signing.</summary>
    public string? AwsSigV4 { get; init; }

    /// <summary><c>CURLOPT_TLSAUTH_TYPE</c>: the origin's TLS authentication method. Default <see langword="null"/>.</summary>
    public string? TlsAuthType { get; init; }

    /// <summary><c>CURLOPT_TLSAUTH_USERNAME</c>: the origin's TLS-SRP user name. Default <see langword="null"/>.</summary>
    public string? TlsAuthUserName { get; init; }

    /// <summary><c>CURLOPT_TLSAUTH_PASSWORD</c>: the origin's TLS-SRP password, a secret. Default <see langword="null"/>.</summary>
    public string? TlsAuthPassword { get; init; }

    /// <summary><c>CURLOPT_PROXY_TLSAUTH_TYPE</c>: the HTTPS proxy's TLS authentication method. Default <see langword="null"/>.</summary>
    public string? ProxyTlsAuthType { get; init; }

    /// <summary><c>CURLOPT_PROXY_TLSAUTH_USERNAME</c>: the HTTPS proxy's TLS-SRP user name. Default <see langword="null"/>.</summary>
    public string? ProxyTlsAuthUserName { get; init; }

    /// <summary><c>CURLOPT_PROXY_TLSAUTH_PASSWORD</c>: the HTTPS proxy's TLS-SRP password, a secret. Default <see langword="null"/>.</summary>
    public string? ProxyTlsAuthPassword { get; init; }

    /// <summary>Curl's <c>--httpsig-algo</c>, with no <c>CURLOPT_*</c>: the HTTP message signature algorithm. Default <see langword="null"/>: unsigned.</summary>
    public string? HttpSignatureAlgorithm { get; init; }

    /// <summary>Curl's <c>--httpsig-key</c>, with no <c>CURLOPT_*</c>: the signature key, a secret. Default <see langword="null"/>.</summary>
    public string? HttpSignatureKey { get; init; }

    /// <summary>Curl's <c>--httpsig-keyid</c>, with no <c>CURLOPT_*</c>: the key identifier. Default <see langword="null"/>.</summary>
    public string? HttpSignatureKeyId { get; init; }

    /// <summary>Curl's <c>--httpsig-headers</c>, with no <c>CURLOPT_*</c>: the components signed. Default <see langword="null"/>: curl's set.</summary>
    public string? HttpSignatureHeaders { get; init; }

    /// <summary>An <see cref="EasyAuthenticationOptions"/> holding every member of this record.</summary>
    /// <returns>The seam's authentication options.</returns>
    EasyAuthenticationOptions IEasyOptionRecord<EasyAuthenticationOptions>.ToEasy() => new()
    {
        Netrc = Netrc,
        NetrcFile = NetrcFile,
        UserPassword = UserPassword,
        UserName = UserName,
        Password = Password,
        LoginOptions = LoginOptions,
        HttpAuthentication = HttpAuthentication,
        ProxyAuthentication = ProxyAuthentication,
        ProxyUserPassword = ProxyUserPassword,
        ProxyUserName = ProxyUserName,
        ProxyPassword = ProxyPassword,
        SaslAuthorizationIdentity = SaslAuthorizationIdentity,
        SaslInitialResponse = SaslInitialResponse,
        XOAuth2Bearer = XOAuth2Bearer,
        DisallowUserNameInUrl = DisallowUserNameInUrl,
        UnrestrictedAuthentication = UnrestrictedAuthentication,
        ServiceName = ServiceName,
        ProxyServiceName = ProxyServiceName,
        GssApiDelegation = GssApiDelegation,
        KerberosLevel = KerberosLevel,
        AwsSigV4 = AwsSigV4,
        TlsAuthType = TlsAuthType,
        TlsAuthUserName = TlsAuthUserName,
        TlsAuthPassword = TlsAuthPassword,
        ProxyTlsAuthType = ProxyTlsAuthType,
        ProxyTlsAuthUserName = ProxyTlsAuthUserName,
        ProxyTlsAuthPassword = ProxyTlsAuthPassword,
        HttpSignatureAlgorithm = HttpSignatureAlgorithm,
        HttpSignatureKey = HttpSignatureKey,
        HttpSignatureKeyId = HttpSignatureKeyId,
        HttpSignatureHeaders = HttpSignatureHeaders,
    };

    /// <summary>Writes every member for the record's generated <see cref="ToString"/>, with each secret redacted so it never reaches a log.</summary>
    /// <param name="builder">Receives the member list.</param>
    /// <returns><see langword="true"/>: the record has members to print.</returns>
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

    /// <summary>The placeholder for a set secret, or <see langword="null"/> for one that is not set, so the output still shows which are set.</summary>
    private static string? Redact(string? secret) => secret is null ? null : RedactedSecret;
}
