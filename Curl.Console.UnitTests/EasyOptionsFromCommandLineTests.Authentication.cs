using Curl.Api.Options.Authentication;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="EasyOptionsFromCommandLine.AuthenticationOf" />: each authentication command-line option
/// given sets its <see cref="EasyAuthenticationOptions" /> member, and a command line without it keeps
/// libcurl's default (ADR-0516).
/// </summary>
public sealed partial class EasyOptionsFromCommandLineTests
{
    private static readonly EasyAuthenticationOptions LibcurlAuthenticationDefaults = new();

    public static IEnumerable<object?[]> GivenAuthenticationOptions =>
    [
        GivenAuthentication("-u", o => o.UserPassword, "alice:secret", "-u", "alice:secret"),
        GivenAuthentication("-u with an empty password", o => o.UserPassword, "alice:", "-u", "alice:"),
        GivenAuthentication("-n", o => o.Netrc, NetrcUsage.Required, "-n"),
        GivenAuthentication("--netrc-file", o => o.NetrcFile, "my.netrc", "--netrc-file", "my.netrc"),
        GivenAuthentication("--netrc-file requires the file", o => o.Netrc, NetrcUsage.Required, "--netrc-file", "my.netrc"),
        GivenAuthentication("--netrc-optional", o => o.Netrc, NetrcUsage.Optional, "--netrc-optional"),
        GivenAuthentication("--netrc-optional outranks -n", o => o.Netrc, NetrcUsage.Optional, "-n", "--netrc-optional"),
        GivenAuthentication("--basic", o => o.HttpAuthentication, HttpAuthenticationMethods.Basic, "--basic"),
        GivenAuthentication("--digest", o => o.HttpAuthentication, HttpAuthenticationMethods.Digest, "--digest"),
        GivenAuthentication("--ntlm", o => o.HttpAuthentication, HttpAuthenticationMethods.Ntlm, "--ntlm"),
        GivenAuthentication("--negotiate", o => o.HttpAuthentication, HttpAuthenticationMethods.Negotiate, "--negotiate"),
        GivenAuthentication("--basic --digest", o => o.HttpAuthentication, HttpAuthenticationMethods.Basic | HttpAuthenticationMethods.Digest, "--basic", "--digest"),
        GivenAuthentication("--digest --no-digest", o => o.HttpAuthentication, HttpAuthenticationMethods.Basic, "--digest", "--no-digest"),
        GivenAuthentication("--anyauth", o => o.HttpAuthentication, HttpAuthenticationMethods.Any, "--anyauth"),
        GivenAuthentication("--anyauth --no-basic", o => o.HttpAuthentication, HttpAuthenticationMethods.Digest | HttpAuthenticationMethods.Ntlm | HttpAuthenticationMethods.Negotiate, "--anyauth", "--no-basic"),
        GivenAuthentication("--ntlm-wb", o => o.HttpAuthentication, HttpAuthenticationMethods.Basic, "--ntlm-wb"),
        GivenAuthentication("--oauth2-bearer", o => o.XOAuth2Bearer, "tok", "--oauth2-bearer", "tok"),
        GivenAuthentication("--oauth2-bearer method", o => o.HttpAuthentication, HttpAuthenticationMethods.Bearer, "--oauth2-bearer", "tok"),
        GivenAuthentication("--aws-sigv4", o => o.AwsSigV4, "aws:amz:us-east-1:s3", "--aws-sigv4", "aws:amz:us-east-1:s3"),
        GivenAuthentication("--aws-sigv4 method", o => o.HttpAuthentication, HttpAuthenticationMethods.Basic | HttpAuthenticationMethods.AwsSigV4, "--aws-sigv4", "aws:amz"),
        GivenAuthentication("-U", o => o.ProxyUserPassword, "bob:hush", "-U", "bob:hush"),
        GivenAuthentication("--proxy-basic", o => o.ProxyAuthentication, HttpAuthenticationMethods.Basic, "--proxy-basic"),
        GivenAuthentication("--proxy-digest", o => o.ProxyAuthentication, HttpAuthenticationMethods.Digest, "--proxy-digest"),
        GivenAuthentication("--proxy-ntlm", o => o.ProxyAuthentication, HttpAuthenticationMethods.Ntlm, "--proxy-ntlm"),
        GivenAuthentication("--proxy-negotiate", o => o.ProxyAuthentication, HttpAuthenticationMethods.Negotiate, "--proxy-negotiate"),
        GivenAuthentication("--proxy-anyauth", o => o.ProxyAuthentication, HttpAuthenticationMethods.Any, "--proxy-anyauth"),
        GivenAuthentication("--login-options", o => o.LoginOptions, "AUTH=NTLM", "--login-options", "AUTH=NTLM"),
        GivenAuthentication("--sasl-authzid", o => o.SaslAuthorizationIdentity, "admin", "--sasl-authzid", "admin"),
        GivenAuthentication("--sasl-ir", o => o.SaslInitialResponse, true, "--sasl-ir"),
        GivenAuthentication("--service-name", o => o.ServiceName, "imap", "--service-name", "imap"),
        GivenAuthentication("--proxy-service-name", o => o.ProxyServiceName, "proxy", "--proxy-service-name", "proxy"),
        GivenAuthentication("--delegation policy", o => o.GssApiDelegation, GssApiDelegation.PolicyFlag, "--delegation", "policy"),
        GivenAuthentication("--delegation always", o => o.GssApiDelegation, GssApiDelegation.Flag, "--delegation", "always"),
        GivenAuthentication("--delegation none", o => o.GssApiDelegation, GssApiDelegation.None, "--delegation", "none"),
        GivenAuthentication("--krb", o => o.KerberosLevel, null, "--krb", "private"),
        GivenAuthentication("--tlsauthtype", o => o.TlsAuthType, null, "--tlsauthtype", "SRP"),
        GivenAuthentication("--tlsuser", o => o.TlsAuthUserName, null, "--tlsuser", "srpuser"),
        GivenAuthentication("--tlspassword", o => o.TlsAuthPassword, null, "--tlspassword", "srppass"),
        GivenAuthentication("--proxy-tlsauthtype", o => o.ProxyTlsAuthType, null, "--proxy-tlsauthtype", "SRP"),
        GivenAuthentication("--proxy-tlsuser", o => o.ProxyTlsAuthUserName, null, "--proxy-tlsuser", "srpuser"),
        GivenAuthentication("--proxy-tlspassword", o => o.ProxyTlsAuthPassword, null, "--proxy-tlspassword", "srppass"),
        GivenAuthentication("--location-trusted", o => o.UnrestrictedAuthentication, true, "--location-trusted"),
        GivenAuthentication("--disallow-username-in-url", o => o.DisallowUserNameInUrl, true, "--disallow-username-in-url"),
        GivenAuthentication("--httpsig-algo", o => o.HttpSignatureAlgorithm, "ed25519", "--httpsig-algo", "ed25519"),
        GivenAuthentication("--httpsig-key", o => o.HttpSignatureKey, "00ff", "--httpsig-key", "00ff"),
        GivenAuthentication("--httpsig-keyid", o => o.HttpSignatureKeyId, "key-1", "--httpsig-keyid", "key-1"),
        GivenAuthentication("--httpsig-headers", o => o.HttpSignatureHeaders, "method authority", "--httpsig-headers", "method authority"),
    ];

    public static IEnumerable<object?[]> AbsentAuthenticationOptions =>
    [
        AbsentAuthentication("Netrc", o => o.Netrc),
        AbsentAuthentication("NetrcFile", o => o.NetrcFile),
        AbsentAuthentication("UserPassword", o => o.UserPassword),
        AbsentAuthentication("UserName", o => o.UserName),
        AbsentAuthentication("Password", o => o.Password),
        AbsentAuthentication("LoginOptions", o => o.LoginOptions),
        AbsentAuthentication("HttpAuthentication", o => o.HttpAuthentication),
        AbsentAuthentication("ProxyAuthentication", o => o.ProxyAuthentication),
        AbsentAuthentication("ProxyUserPassword", o => o.ProxyUserPassword),
        AbsentAuthentication("ProxyUserName", o => o.ProxyUserName),
        AbsentAuthentication("ProxyPassword", o => o.ProxyPassword),
        AbsentAuthentication("SaslAuthorizationIdentity", o => o.SaslAuthorizationIdentity),
        AbsentAuthentication("SaslInitialResponse", o => o.SaslInitialResponse),
        AbsentAuthentication("XOAuth2Bearer", o => o.XOAuth2Bearer),
        AbsentAuthentication("DisallowUserNameInUrl", o => o.DisallowUserNameInUrl),
        AbsentAuthentication("UnrestrictedAuthentication", o => o.UnrestrictedAuthentication),
        AbsentAuthentication("ServiceName", o => o.ServiceName),
        AbsentAuthentication("ProxyServiceName", o => o.ProxyServiceName),
        AbsentAuthentication("GssApiDelegation", o => o.GssApiDelegation),
        AbsentAuthentication("KerberosLevel", o => o.KerberosLevel),
        AbsentAuthentication("AwsSigV4", o => o.AwsSigV4),
        AbsentAuthentication("TlsAuthType", o => o.TlsAuthType),
        AbsentAuthentication("ProxyTlsAuthType", o => o.ProxyTlsAuthType),
        AbsentAuthentication("HttpSignatureAlgorithm", o => o.HttpSignatureAlgorithm),
        AbsentAuthentication("HttpSignatureKey", o => o.HttpSignatureKey),
        AbsentAuthentication("HttpSignatureKeyId", o => o.HttpSignatureKeyId),
        AbsentAuthentication("HttpSignatureHeaders", o => o.HttpSignatureHeaders),
    ];

    [TestMethod]
    [DynamicData(nameof(GivenAuthenticationOptions))]
    public void AuthenticationOf_OptionGiven_SetsItsMember(string option, Func<EasyAuthenticationOptions, object?> read, object? expected, string[] arguments)
    {
        EasyAuthenticationOptions authentication = EasyOptionsFromCommandLine.AuthenticationOf(Parse(arguments));

        Assert.AreEqual(expected, read(authentication), option);
    }

    [TestMethod]
    [DynamicData(nameof(AbsentAuthenticationOptions))]
    public void AuthenticationOf_OptionAbsent_KeepsLibcurlDefault(string member, Func<EasyAuthenticationOptions, object?> read)
    {
        EasyAuthenticationOptions authentication = EasyOptionsFromCommandLine.AuthenticationOf(Parse());

        Assert.AreEqual(read(LibcurlAuthenticationDefaults), read(authentication), member);
    }

    [TestMethod]
    public void AuthenticationOf_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => EasyOptionsFromCommandLine.AuthenticationOf(null!));
    }

    private static object?[] GivenAuthentication(string option, Func<EasyAuthenticationOptions, object?> read, object? expected, params string[] arguments) =>
        [option, read, expected, arguments];

    private static object?[] AbsentAuthentication(string member, Func<EasyAuthenticationOptions, object?> read) => [member, read];
}
