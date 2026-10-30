using System.Globalization;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;

namespace Libcurl.Options;

/// <summary>
/// The setter rows of every <c>CURLOPT_*</c> <see cref="EasyAuthenticationOptions"/> holds (<c>.netrc</c>,
/// credentials, the <c>CURLAUTH_*</c> bitmasks, SASL, the bearer token, service names, GSS-API delegation,
/// Kerberos, AWS SigV4, HTTP message signatures and TLS-SRP, with the proxy twins), each checking its value as curl 8.22.0's
/// <c>lib/setopt.c</c> does (ADR-0574).
/// </summary>
internal static class EasyAuthenticationOptionSetters
{
    private const string SecureRemotePassword = "SRP";

    /// <summary>The <c>--httpsig-algo</c> text of each <c>CURLHTTPSIG_*</c> number, <see langword="null"/> for <c>CURLHTTPSIG_NONE</c>.</summary>
    private static readonly string?[] HttpSignatureAlgorithmNames = [null, "ed25519", "hmac-sha256"];

    /// <summary><c>CURLAUTH_BASIC</c>, the lowest method bit.</summary>
    private const long LowestAuthenticationBit = 1L << 0;

    /// <summary><c>CURLAUTH_ONLY</c>, a modifier rather than a method; every bit below it is a method.</summary>
    private const long OnlyBit = 1L << 31;

    /// <summary>Adds the authentication rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        AddCredentialSetters(setters);
        AddMethodSetters(setters);
        AddTlsAuthenticationSetters(setters);
    }

    private static void AddCredentialSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Netrc] = new(SetNetrc);
        setters[CurlOption.NetrcFile] = new(SetNetrcFile);
        setters[CurlOption.Userpwd] = new(SetUserPassword);
        setters[CurlOption.Username] = new(SetUserName);
        setters[CurlOption.Password] = new(SetPassword);
        setters[CurlOption.LoginOptions] = new(SetLoginOptions);
        setters[CurlOption.Proxyuserpwd] = new(SetProxyUserPassword);
        setters[CurlOption.Proxyusername] = new(SetProxyUserName);
        setters[CurlOption.Proxypassword] = new(SetProxyPassword);
        setters[CurlOption.SaslAuthzid] = new(SetSaslAuthorizationIdentity);
        setters[CurlOption.SaslIr] = new(SetSaslInitialResponse);
        setters[CurlOption.Xoauth2Bearer] = new(SetXOAuth2Bearer);
        setters[CurlOption.DisallowUsernameInUrl] = new(SetDisallowUserNameInUrl);
        setters[CurlOption.UnrestrictedAuth] = new(SetUnrestrictedAuthentication);
    }

    private static void AddMethodSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Httpauth] = new(SetHttpAuthentication);
        setters[CurlOption.Proxyauth] = new(SetProxyAuthentication);
        setters[CurlOption.ServiceName] = new(SetServiceName);
        setters[CurlOption.ProxyServiceName] = new(SetProxyServiceName);
        setters[CurlOption.GssapiDelegation] = new(SetGssApiDelegation);
        setters[CurlOption.Krblevel] = new(SetKerberosLevel);
        setters[CurlOption.AwsSigv4] = new(SetAwsSigV4);
        setters[CurlOption.HttpsigAlgorithm] = new(SetHttpSignatureAlgorithm);
        setters[CurlOption.HttpsigKey] = new(SetHttpSignatureKey);
        setters[CurlOption.HttpsigKeyid] = new(SetHttpSignatureKeyId);
        setters[CurlOption.HttpsigHeaders] = new(SetHttpSignatureHeaders);
    }

    private static void AddTlsAuthenticationSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.TlsauthType] = new(SetTlsAuthType);
        setters[CurlOption.TlsauthUsername] = new(SetTlsAuthUserName);
        setters[CurlOption.TlsauthPassword] = new(SetTlsAuthPassword);
        setters[CurlOption.ProxyTlsauthType] = new(SetProxyTlsAuthType);
        setters[CurlOption.ProxyTlsauthUsername] = new(SetProxyTlsAuthUserName);
        setters[CurlOption.ProxyTlsauthPassword] = new(SetProxyTlsAuthPassword);
    }

    /// <summary><c>CURLOPT_NETRC</c>: 0 to 2 (<c>CURL_NETRC_*</c>), else <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetNetrc(ref EasyOptions o, object? v)
    {
        var n = NumberOf(v);
        return n >= (long)NetrcUsage.Ignored && n <= (long)NetrcUsage.Required
            ? Change(ref o, a => a with { Netrc = (NetrcUsage)n })
            : CurlCode.BadFunctionArgument;
    }

    private static CurlCode SetNetrcFile(ref EasyOptions o, object? v) => Change(ref o, a => a with { NetrcFile = (string?)v });

    private static CurlCode SetUserPassword(ref EasyOptions o, object? v) => Change(ref o, a => a with { UserPassword = (string?)v });

    private static CurlCode SetUserName(ref EasyOptions o, object? v) => Change(ref o, a => a with { UserName = (string?)v });

    private static CurlCode SetPassword(ref EasyOptions o, object? v) => Change(ref o, a => a with { Password = (string?)v });

    private static CurlCode SetLoginOptions(ref EasyOptions o, object? v) => Change(ref o, a => a with { LoginOptions = (string?)v });

    private static CurlCode SetProxyUserPassword(ref EasyOptions o, object? v) => Change(ref o, a => a with { ProxyUserPassword = (string?)v });

    private static CurlCode SetProxyUserName(ref EasyOptions o, object? v) => Change(ref o, a => a with { ProxyUserName = (string?)v });

    private static CurlCode SetProxyPassword(ref EasyOptions o, object? v) => Change(ref o, a => a with { ProxyPassword = (string?)v });

    private static CurlCode SetSaslAuthorizationIdentity(ref EasyOptions o, object? v) => Change(ref o, a => a with { SaslAuthorizationIdentity = (string?)v });

    private static CurlCode SetSaslInitialResponse(ref EasyOptions o, object? v) => Change(ref o, a => a with { SaslInitialResponse = IsOn(v) });

    private static CurlCode SetXOAuth2Bearer(ref EasyOptions o, object? v) => Change(ref o, a => a with { XOAuth2Bearer = (string?)v });

    private static CurlCode SetDisallowUserNameInUrl(ref EasyOptions o, object? v) => Change(ref o, a => a with { DisallowUserNameInUrl = IsOn(v) });

    private static CurlCode SetUnrestrictedAuthentication(ref EasyOptions o, object? v) => Change(ref o, a => a with { UnrestrictedAuthentication = IsOn(v) });

    /// <summary><c>CURLOPT_HTTPAUTH</c>: the <c>CURLAUTH_*</c> bitmask; see <see cref="TryMethods"/>.</summary>
    private static CurlCode SetHttpAuthentication(ref EasyOptions o, object? v)
    {
        var code = TryMethods(v, out var methods);
        return code == CurlCode.Ok ? Change(ref o, a => a with { HttpAuthentication = methods }) : code;
    }

    /// <summary><c>CURLOPT_PROXYAUTH</c>: the <c>CURLAUTH_*</c> bitmask; see <see cref="TryMethods"/>.</summary>
    private static CurlCode SetProxyAuthentication(ref EasyOptions o, object? v)
    {
        var code = TryMethods(v, out var methods);
        return code == CurlCode.Ok ? Change(ref o, a => a with { ProxyAuthentication = methods }) : code;
    }

    private static CurlCode SetServiceName(ref EasyOptions o, object? v) => Change(ref o, a => a with { ServiceName = (string?)v });

    private static CurlCode SetProxyServiceName(ref EasyOptions o, object? v) => Change(ref o, a => a with { ProxyServiceName = (string?)v });

    /// <summary><c>CURLOPT_GSSAPI_DELEGATION</c>: only the two <c>CURLGSSAPI_DELEGATION_*</c> bits are kept, as libcurl masks them.</summary>
    private static CurlCode SetGssApiDelegation(ref EasyOptions o, object? v)
    {
        var bits = (GssApiDelegation)(NumberOf(v) & (long)(GssApiDelegation.PolicyFlag | GssApiDelegation.Flag));
        return Change(ref o, a => a with { GssApiDelegation = bits });
    }

    /// <summary><c>CURLOPT_KRBLEVEL</c>: <see cref="CurlCode.NotBuiltIn"/> for every value, as curl 8.22.0 answers since FTP Kerberos was removed in 8.17.0 (ADR-0995).</summary>
    private static CurlCode SetKerberosLevel(ref EasyOptions o, object? v) => CurlCode.NotBuiltIn;

    /// <summary><c>CURLOPT_AWS_SIGV4</c>: a string also sets <see cref="EasyAuthenticationOptions.HttpAuthentication"/> to <c>CURLAUTH_AWS_SIGV4</c>, as libcurl does.</summary>
    private static CurlCode SetAwsSigV4(ref EasyOptions o, object? v) =>
        v is null
            ? Change(ref o, a => a with { AwsSigV4 = null })
            : Change(ref o, a => a with { AwsSigV4 = (string)v, HttpAuthentication = HttpAuthenticationMethods.AwsSigV4 });

    /// <summary>
    /// <c>CURLOPT_HTTPSIG_ALGORITHM</c>: <c>CURLHTTPSIG_NONE</c> (0) clears the algorithm, <c>CURLHTTPSIG_ED25519</c> (1) and
    /// <c>CURLHTTPSIG_HMAC_SHA256</c> (2) name it as the <c>--httpsig-algo</c> text; any other number is <see cref="CurlCode.BadFunctionArgument"/>.
    /// </summary>
    private static CurlCode SetHttpSignatureAlgorithm(ref EasyOptions o, object? v)
    {
        var number = NumberOf(v);
        return number is < 0 or > 2
            ? CurlCode.BadFunctionArgument
            : Change(ref o, a => a with { HttpSignatureAlgorithm = HttpSignatureAlgorithmNames[number] });
    }

    private static CurlCode SetHttpSignatureKey(ref EasyOptions o, object? v) => Change(ref o, a => a with { HttpSignatureKey = (string?)v });

    private static CurlCode SetHttpSignatureKeyId(ref EasyOptions o, object? v) => Change(ref o, a => a with { HttpSignatureKeyId = (string?)v });

    private static CurlCode SetHttpSignatureHeaders(ref EasyOptions o, object? v) => Change(ref o, a => a with { HttpSignatureHeaders = (string?)v });

    /// <summary><c>CURLOPT_TLSAUTH_TYPE</c>: <c>SRP</c> in any case, or <see langword="null"/>; anything else is <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetTlsAuthType(ref EasyOptions o, object? v) =>
        !SrpIsBuiltIn ? CurlCode.NotBuiltIn : IsTlsAuthType(v) ? Change(ref o, a => a with { TlsAuthType = (string?)v }) : CurlCode.BadFunctionArgument;

    private static CurlCode SetTlsAuthUserName(ref EasyOptions o, object? v) => SrpIsBuiltIn ? Change(ref o, a => a with { TlsAuthUserName = (string?)v }) : CurlCode.NotBuiltIn;

    private static CurlCode SetTlsAuthPassword(ref EasyOptions o, object? v) => SrpIsBuiltIn ? Change(ref o, a => a with { TlsAuthPassword = (string?)v }) : CurlCode.NotBuiltIn;

    /// <summary><c>CURLOPT_PROXY_TLSAUTH_TYPE</c>: <c>SRP</c> in any case, or <see langword="null"/>; anything else is <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetProxyTlsAuthType(ref EasyOptions o, object? v) =>
        !SrpIsBuiltIn ? CurlCode.NotBuiltIn : IsTlsAuthType(v) ? Change(ref o, a => a with { ProxyTlsAuthType = (string?)v }) : CurlCode.BadFunctionArgument;

    private static CurlCode SetProxyTlsAuthUserName(ref EasyOptions o, object? v) => SrpIsBuiltIn ? Change(ref o, a => a with { ProxyTlsAuthUserName = (string?)v }) : CurlCode.NotBuiltIn;

    private static CurlCode SetProxyTlsAuthPassword(ref EasyOptions o, object? v) => SrpIsBuiltIn ? Change(ref o, a => a with { ProxyTlsAuthPassword = (string?)v }) : CurlCode.NotBuiltIn;

    /// <summary>
    /// Turns a <c>CURLAUTH_*</c> bitmask into <see cref="HttpAuthenticationMethods"/>: a value with a bit
    /// beyond the 32 the type carries (a 32-bit value sign-extended, as <c>CURLAUTH_ANY</c> is in a 64-bit
    /// <c>long</c>, still fits) is <see cref="CurlCode.BadFunctionArgument"/>; a mask with no method bit below
    /// <c>CURLAUTH_ONLY</c> but not 0 is <see cref="CurlCode.NotBuiltIn"/>, as libcurl answers it.
    /// </summary>
    private static CurlCode TryMethods(object? value, out HttpAuthenticationMethods methods)
    {
        var n = NumberOf(value);
        methods = (HttpAuthenticationMethods)unchecked((uint)n);
        if (n < int.MinValue || n > uint.MaxValue)
        {
            return CurlCode.BadFunctionArgument;
        }

        return methods == HttpAuthenticationMethods.None || (n & (OnlyBit - LowestAuthenticationBit)) != 0
            ? CurlCode.Ok
            : CurlCode.NotBuiltIn;
    }

    /// <summary>Whether this platform's curl has TLS-SRP: a Schannel build (Windows) has none and answers <see cref="CurlCode.NotBuiltIn"/> for every <c>TLSAUTH_*</c> option.</summary>
    internal static bool SrpIsBuiltIn { get; set; } = !OperatingSystem.IsWindows();

    /// <summary>Whether a <c>TLSAUTH_TYPE</c> value is acceptable: <see langword="null"/> (none) or <c>SRP</c> in any case, the one method libcurl knows.</summary>
    private static bool IsTlsAuthType(object? value) =>
        value is null || string.Equals((string)value, SecureRemotePassword, StringComparison.OrdinalIgnoreCase);

    /// <summary>Replaces the handle's <see cref="EasyAuthenticationOptions"/> with the copy <paramref name="change"/> returns; always <see cref="CurlCode.Ok"/>.</summary>
    private static CurlCode Change(ref EasyOptions options, Func<EasyAuthenticationOptions, EasyAuthenticationOptions> change)
    {
        options = options with { Authentication = change(options.Authentication) };
        return CurlCode.Ok;
    }

    /// <summary>Whether a <c>CURLOT_LONG</c> on/off value is on: any value but 0, as in libcurl.</summary>
    private static bool IsOn(object? value) => NumberOf(value) != 0;

    private static long NumberOf(object? value) => Convert.ToInt64(value, CultureInfo.InvariantCulture);
}
