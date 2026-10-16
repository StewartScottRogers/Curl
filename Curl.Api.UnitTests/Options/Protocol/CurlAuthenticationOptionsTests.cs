using Curl.Api.Client;
using Curl.Api.Options.Authentication;

namespace Curl.Api.UnitTests.Options.Protocol;

[TestClass]
public sealed class CurlAuthenticationOptionsTests
{
    private static readonly CurlAuthenticationOptions EverySecretSet = new()
    {
        UserPassword = "user:userpwd-secret",
        Password = "password-secret",
        ProxyUserPassword = "proxy:proxyuserpwd-secret",
        ProxyPassword = "proxypassword-secret",
        XOAuth2Bearer = "bearer-secret",
        TlsAuthPassword = "tlsauth-secret",
        ProxyTlsAuthPassword = "proxytlsauth-secret",
        HttpSignatureKey = "signaturekey-secret",
    };

    private static async Task<EasyAuthenticationOptions> ReceivedByEngine(CurlAuthenticationOptions authentication)
    {
        FakeCurlEngine engine = new();
        await using CurlClient client = new(engine, new CurlClientOptions { Authentication = authentication }, ownsEngine: true);
        client.CreateEasy();
        return engine.CreatedEasyHandles.Single().Options.Authentication;
    }

    [TestMethod]
    public async Task EveryMember_ReachesTheEngineUnderItsEasyName()
    {
        CurlAuthenticationOptions authentication = new()
        {
            Netrc = NetrcUsage.Required,
            NetrcFile = "netrc",
            UserPassword = "u:p",
            UserName = "user",
            Password = "pass",
            LoginOptions = "AUTH=NTLM",
            HttpAuthentication = HttpAuthenticationMethods.Digest,
            ProxyAuthentication = HttpAuthenticationMethods.Ntlm,
            ProxyUserPassword = "pu:pp",
            ProxyUserName = "proxy-user",
            ProxyPassword = "proxy-pass",
            SaslAuthorizationIdentity = "authz",
            SaslInitialResponse = true,
            XOAuth2Bearer = "token",
            DisallowUserNameInUrl = true,
            UnrestrictedAuthentication = true,
            ServiceName = "service",
            ProxyServiceName = "proxy-service",
            GssApiDelegation = GssApiDelegation.Flag,
            KerberosLevel = "private",
            AwsSigV4 = "aws:amz:us-east-1:s3",
            TlsAuthType = "SRP",
            TlsAuthUserName = "srp-user",
            TlsAuthPassword = "srp-pass",
            ProxyTlsAuthType = "SRP",
            ProxyTlsAuthUserName = "proxy-srp-user",
            ProxyTlsAuthPassword = "proxy-srp-pass",
            HttpSignatureAlgorithm = "ed25519",
            HttpSignatureKey = "abcd",
            HttpSignatureKeyId = "key-1",
            HttpSignatureHeaders = "@method",
        };

        EasyAuthenticationOptions easy = await ReceivedByEngine(authentication);

        Assert.AreEqual(NetrcUsage.Required, easy.Netrc);
        Assert.AreEqual("netrc", easy.NetrcFile);
        Assert.AreEqual("u:p", easy.UserPassword);
        Assert.AreEqual("user", easy.UserName);
        Assert.AreEqual("pass", easy.Password);
        Assert.AreEqual("AUTH=NTLM", easy.LoginOptions);
        Assert.AreEqual(HttpAuthenticationMethods.Digest, easy.HttpAuthentication);
        Assert.AreEqual(HttpAuthenticationMethods.Ntlm, easy.ProxyAuthentication);
        Assert.AreEqual("pu:pp", easy.ProxyUserPassword);
        Assert.AreEqual("proxy-user", easy.ProxyUserName);
        Assert.AreEqual("proxy-pass", easy.ProxyPassword);
        Assert.AreEqual("authz", easy.SaslAuthorizationIdentity);
        Assert.IsTrue(easy.SaslInitialResponse);
        Assert.AreEqual("token", easy.XOAuth2Bearer);
        Assert.IsTrue(easy.DisallowUserNameInUrl);
        Assert.IsTrue(easy.UnrestrictedAuthentication);
        Assert.AreEqual("service", easy.ServiceName);
        Assert.AreEqual("proxy-service", easy.ProxyServiceName);
        Assert.AreEqual(GssApiDelegation.Flag, easy.GssApiDelegation);
        Assert.AreEqual("private", easy.KerberosLevel);
        Assert.AreEqual("aws:amz:us-east-1:s3", easy.AwsSigV4);
        Assert.AreEqual("SRP", easy.TlsAuthType);
        Assert.AreEqual("srp-user", easy.TlsAuthUserName);
        Assert.AreEqual("srp-pass", easy.TlsAuthPassword);
        Assert.AreEqual("SRP", easy.ProxyTlsAuthType);
        Assert.AreEqual("proxy-srp-user", easy.ProxyTlsAuthUserName);
        Assert.AreEqual("proxy-srp-pass", easy.ProxyTlsAuthPassword);
        Assert.AreEqual("ed25519", easy.HttpSignatureAlgorithm);
        Assert.AreEqual("abcd", easy.HttpSignatureKey);
        Assert.AreEqual("key-1", easy.HttpSignatureKeyId);
        Assert.AreEqual("@method", easy.HttpSignatureHeaders);
    }

    [TestMethod]
    public async Task DefaultRecord_LeavesTheEngineAtLibcurlDefaults()
    {
        EasyAuthenticationOptions easy = await ReceivedByEngine(new CurlAuthenticationOptions());

        Assert.AreEqual(new EasyAuthenticationOptions(), easy);
    }

    [TestMethod]
    public void ToString_RedactsEverySecretThatIsSet()
    {
        string text = (EverySecretSet with { UserName = "visible-user" }).ToString();

        Assert.DoesNotContain("secret", text);
        Assert.Contains("Password = <redacted>", text);
        Assert.Contains("XOAuth2Bearer = <redacted>", text);
        Assert.Contains("HttpSignatureKey = <redacted>", text);
        Assert.Contains("UserName = visible-user", text);
    }

    [TestMethod]
    public void ToString_PrintsNothingForAnUnsetSecret()
    {
        string text = new CurlAuthenticationOptions().ToString();

        Assert.DoesNotContain("<redacted>", text);
        Assert.Contains("Password = ,", text);
    }

    [TestMethod]
    public void InvalidValues_ThrowAsTheyAreSet()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlAuthenticationOptions { Netrc = (NetrcUsage)3 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlAuthenticationOptions { GssApiDelegation = (GssApiDelegation)3 });
    }
}
