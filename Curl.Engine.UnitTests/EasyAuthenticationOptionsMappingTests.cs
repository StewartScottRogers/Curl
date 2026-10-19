using Curl.Console;
using System.Net;

using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Protocols;
using Curl.Api.Options.Proxy;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Engine;

/// <summary>
/// Pins that <see cref="HttpRequestOptionsMapping" />, <see cref="HttpSignatureSetup" />,
/// <see cref="MailRequestOptionsMapping" />, <see cref="RedirectPolicyMapping" /> and
/// <see cref="TransferContextFactory" /> read their authentication settings from an
/// <see cref="EasyAuthenticationOptions" /> record built directly, as a libcurl caller sets it (BL-2352).
/// </summary>
[TestClass]
public sealed class EasyAuthenticationOptionsMappingTests
{
    private static readonly CommandLineOptions NoOptions = CommandLineParser.Parse(["http://example.com/"], _ => true).Options!;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(HttpAuthenticationMethods.Any, null, HttpAuthSchemes.Any)]
    [DataRow(HttpAuthenticationMethods.Any, "tok", HttpAuthSchemes.Any | HttpAuthSchemes.Bearer)]
    [DataRow(HttpAuthenticationMethods.Basic | HttpAuthenticationMethods.Digest, null, HttpAuthSchemes.Basic | HttpAuthSchemes.Digest)]
    [DataRow(HttpAuthenticationMethods.Ntlm | HttpAuthenticationMethods.Negotiate | HttpAuthenticationMethods.Bearer, "tok", HttpAuthSchemes.Ntlm | HttpAuthSchemes.Negotiate | HttpAuthSchemes.Bearer)]
    [DataRow(HttpAuthenticationMethods.Bearer, null, HttpAuthSchemes.Basic)]
    [DataRow(HttpAuthenticationMethods.AwsSigV4, null, HttpAuthSchemes.None)]
    [DataRow(HttpAuthenticationMethods.Basic | HttpAuthenticationMethods.AwsSigV4, null, HttpAuthSchemes.Basic)]
    public void AuthSchemesOf_Methods_GiveTheSchemesTheCommandLineGives(HttpAuthenticationMethods methods, string? bearer, HttpAuthSchemes expected)
    {
        EasyAuthenticationOptions authentication = new() { HttpAuthentication = methods, XOAuth2Bearer = bearer };
        Diagnostics.Arrange("methods / bearer", $"{methods} / {bearer}");

        HttpAuthSchemes schemes = HttpRequestOptionsMapping.AuthSchemesOf(authentication);
        Diagnostics.Act("schemes", schemes);

        Diagnostics.Assert("schemes", expected, schemes);
        Assert.AreEqual(expected, schemes);
    }

    [TestMethod]
    public void HttpRequestOptionsOf_Record_CopiesTheBearerTokenAndAwsSigV4()
    {
        EasyAuthenticationOptions authentication = new()
        {
            HttpAuthentication = HttpAuthenticationMethods.Digest,
            XOAuth2Bearer = "tok",
            AwsSigV4 = "aws:amz:r:s",
        };
        Diagnostics.Arrange("record", authentication);

        HttpRequestOptions mapped = HttpRequestOptionsMapping.Of(EasyOptionsFromCommandLine.TransferSettingsOf(NoOptions), new EasyHttpOptions(), new EasyBehaviourOptions(), new EasyTransferOptions(), authentication);
        Diagnostics.Act("auth schemes / aws", $"{mapped.AuthSchemes} / {mapped.AwsSigV4}");

        Assert.AreEqual(HttpAuthSchemes.Digest, mapped.AuthSchemes);
        Assert.AreEqual("tok", mapped.BearerToken);
        Assert.AreEqual("aws:amz:r:s", mapped.AwsSigV4);
        Assert.IsNull(mapped.HttpSignature);
    }

    [TestMethod]
    public void HttpRequestOptionsOf_HttpSignatureRecord_SignsAndLeavesNoScheme()
    {
        EasyAuthenticationOptions authentication = new()
        {
            HttpSignatureAlgorithm = "HMAC-SHA256",
            HttpSignatureKey = "00112233",
            HttpSignatureKeyId = "k1",
            HttpSignatureHeaders = "@method",
        };
        Diagnostics.Arrange("record", authentication);

        HttpRequestOptions mapped = HttpRequestOptionsMapping.Of(EasyOptionsFromCommandLine.TransferSettingsOf(NoOptions), new EasyHttpOptions(), new EasyBehaviourOptions(), new EasyTransferOptions(), authentication);
        Diagnostics.Act("auth schemes / signature", $"{mapped.AuthSchemes} / {mapped.HttpSignature}");

        Assert.AreEqual(HttpAuthSchemes.None, mapped.AuthSchemes);
        Assert.AreEqual(new HttpSignatureSettings("hmac-sha256", "00112233", "k1", "@method"), mapped.HttpSignature);
    }

    [TestMethod]
    [DataRow(null, "k1", "curl: --httpsig-key is required")]
    [DataRow("00112233", null, "curl: --httpsig-keyid is required")]
    public void HttpSignatureSetupOf_RecordMissingASetting_RefusesWithExit2(string? key, string? keyId, string expectedLine)
    {
        EasyAuthenticationOptions authentication = new() { HttpSignatureKey = key, HttpSignatureKeyId = keyId };
        Diagnostics.Arrange("key / key id", $"{key} / {keyId}");

        HttpSignatureSetup setup = HttpSignatureSetup.Of(authentication);
        Diagnostics.Act("exit / line", $"{setup.ExitCode} / {setup.ErrorLine}");

        Assert.AreEqual(CurlExitCode.FailedInit, setup.ExitCode);
        Assert.AreEqual(expectedLine, setup.ErrorLine);
        Assert.IsNull(setup.Settings);
    }

    [TestMethod]
    public void MailFromCommandLine_Record_CopiesTheLoginSaslBearerAndServiceName()
    {
        EasyAuthenticationOptions authentication = new()
        {
            LoginOptions = "AUTH=PLAIN",
            SaslAuthorizationIdentity = "boss",
            SaslInitialResponse = true,
            XOAuth2Bearer = "tok",
            ServiceName = "svc",
        };
        Diagnostics.Arrange("record", authentication);

        MailRequestOptions mail = MailRequestOptionsMapping.FromCommandLine(EasyOptionsFromCommandLine.TransferSettingsOf(NoOptions), new EasyTransferOptions(), new EasySmtpOptions(), authentication, "imap")!;
        Diagnostics.Act("login / authzid / ir / service", $"{mail.LoginOptions} / {mail.SaslAuthorizationIdentity} / {mail.SaslInitialResponse} / {mail.ServiceName}");

        Assert.AreEqual("AUTH=PLAIN", mail.LoginOptions);
        Assert.AreEqual("boss", mail.SaslAuthorizationIdentity);
        Assert.IsTrue(mail.SaslInitialResponse);
        Assert.AreEqual("tok", mail.BearerToken);
        Assert.AreEqual("svc", mail.ServiceName);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void RedirectPolicyOf_Record_ReadsLocationTrustedAndDisallowUserName(bool unrestricted, bool disallowUserName)
    {
        EasyAuthenticationOptions authentication = new() { UnrestrictedAuthentication = unrestricted, DisallowUserNameInUrl = disallowUserName };
        Diagnostics.Arrange("record", authentication);

        RedirectPolicy policy = RedirectPolicyMapping.Of(EasyOptionsFromCommandLine.TransferSettingsOf(NoOptions), new EasyHttpOptions(), authentication);
        Diagnostics.Act("trusted / disallows user", $"{policy.LocationTrusted} / {policy.DisallowsUserInUrl}");

        Assert.AreEqual(unrestricted, policy.LocationTrusted);
        Assert.AreEqual(disallowUserName, policy.DisallowsUserInUrl);
    }

    [TestMethod]
    [DataRow("u:p:x", "u", "p:x")]
    [DataRow("u:", "u", "")]
    [DataRow(":p", "", "p")]
    [DataRow("u", "u", "")]
    public void Create_UserPassword_IsSplitAtTheFirstColon(string userPassword, string expectedUser, string expectedPassword)
    {
        Diagnostics.Arrange("user password", userPassword);

        NetworkCredential? credentials = Create(new EasyAuthenticationOptions { UserPassword = userPassword }).Credentials;
        Diagnostics.Act("user / password", $"{credentials?.UserName} / {credentials?.Password}");

        Assert.AreEqual(expectedUser, credentials!.UserName);
        Assert.AreEqual(expectedPassword, credentials.Password);
    }

    [TestMethod]
    public void Create_NoUserPassword_CarriesNoCredentials()
    {
        Diagnostics.Arrange("record", "default");

        NetworkCredential? credentials = Create(new EasyAuthenticationOptions()).Credentials;
        Diagnostics.Act("credentials", credentials);

        Assert.IsNull(credentials);
    }

    [TestMethod]
    public void Create_SmtpUrl_CarriesTheRecordsMailSettings()
    {
        Diagnostics.Arrange("record", "XOAuth2Bearer tok");

        MailRequestOptions? mail = Create(new EasyAuthenticationOptions { XOAuth2Bearer = "tok" }, "smtp://h/").Mail;
        Diagnostics.Act("bearer", mail?.BearerToken);

        Assert.AreEqual("tok", mail!.BearerToken);
    }

    private static TransferContext Create(EasyAuthenticationOptions authentication, string url = "http://h/")
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        return new TransferContextFactory(standardInput).Create(
            EasyOptionsFromCommandLine.TransferSettingsOf(NoOptions),
            new EasyHttpOptions(),
            new EasyBehaviourOptions(),
            new EasyTransferOptions(),
            new EasyProtocolOptions(),
            new EasyProxyOptions(),
            authentication,
            new EasyNetworkOptions(),
            CurlUrl.Parse(url),
            output,
            null,
            null,
            null);
    }
}
