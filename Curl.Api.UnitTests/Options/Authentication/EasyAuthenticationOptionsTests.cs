namespace Curl.Api.Options.Authentication;

[TestClass]
public sealed class EasyAuthenticationOptionsTests
{
    private static readonly EasyAuthenticationOptions Defaults = new();

    [TestMethod]
    public void New_EasyAuthenticationOptions_IgnoresTheNetrcFile()
    {
        Assert.AreEqual(NetrcUsage.Ignored, Defaults.Netrc);
        Assert.IsNull(Defaults.NetrcFile);
    }

    [TestMethod]
    public void New_EasyAuthenticationOptions_LeavesEveryCredentialUnset()
    {
        Assert.IsNull(Defaults.UserPassword);
        Assert.IsNull(Defaults.UserName);
        Assert.IsNull(Defaults.Password);
        Assert.IsNull(Defaults.LoginOptions);
        Assert.IsNull(Defaults.ProxyUserPassword);
        Assert.IsNull(Defaults.ProxyUserName);
        Assert.IsNull(Defaults.ProxyPassword);
        Assert.IsNull(Defaults.SaslAuthorizationIdentity);
        Assert.IsNull(Defaults.XOAuth2Bearer);
        Assert.IsNull(Defaults.AwsSigV4);
    }

    [TestMethod]
    public void New_EasyAuthenticationOptions_OffersBasicToTheOriginAndTheProxy()
    {
        Assert.AreEqual(HttpAuthenticationMethods.Basic, Defaults.HttpAuthentication);
        Assert.AreEqual(HttpAuthenticationMethods.Basic, Defaults.ProxyAuthentication);
    }

    [TestMethod]
    public void New_EasyAuthenticationOptions_LeavesEverySwitchOff()
    {
        Assert.IsFalse(Defaults.SaslInitialResponse);
        Assert.IsFalse(Defaults.DisallowUserNameInUrl);
        Assert.IsFalse(Defaults.UnrestrictedAuthentication);
    }

    [TestMethod]
    public void New_EasyAuthenticationOptions_LeavesGssApiAndKerberosAtLibcurlsDefaults()
    {
        Assert.IsNull(Defaults.ServiceName);
        Assert.IsNull(Defaults.ProxyServiceName);
        Assert.AreEqual(GssApiDelegation.None, Defaults.GssApiDelegation);
        Assert.IsNull(Defaults.KerberosLevel);
    }

    [TestMethod]
    public void New_EasyAuthenticationOptions_LeavesTlsSrpUnsetForTheOriginAndTheProxy()
    {
        Assert.IsNull(Defaults.TlsAuthType);
        Assert.IsNull(Defaults.TlsAuthUserName);
        Assert.IsNull(Defaults.TlsAuthPassword);
        Assert.IsNull(Defaults.ProxyTlsAuthType);
        Assert.IsNull(Defaults.ProxyTlsAuthUserName);
        Assert.IsNull(Defaults.ProxyTlsAuthPassword);
    }

    [TestMethod]
    public void HttpAuthenticationMethods_Members_HoldLibcurlsCurlauthBits()
    {
        Assert.AreEqual(0u, (uint)HttpAuthenticationMethods.None);
        Assert.AreEqual(1u, (uint)HttpAuthenticationMethods.Basic);
        Assert.AreEqual(2u, (uint)HttpAuthenticationMethods.Digest);
        Assert.AreEqual(4u, (uint)HttpAuthenticationMethods.Negotiate);
        Assert.AreEqual(8u, (uint)HttpAuthenticationMethods.Ntlm);
        Assert.AreEqual(16u, (uint)HttpAuthenticationMethods.DigestIe);
        Assert.AreEqual(32u, (uint)HttpAuthenticationMethods.NtlmWb);
        Assert.AreEqual(64u, (uint)HttpAuthenticationMethods.Bearer);
        Assert.AreEqual(128u, (uint)HttpAuthenticationMethods.AwsSigV4);
        Assert.AreEqual(0x80000000u, (uint)HttpAuthenticationMethods.Only);
        Assert.AreEqual(0xFFFFFFEFu, (uint)HttpAuthenticationMethods.Any);
        Assert.AreEqual(0xFFFFFFEEu, (uint)HttpAuthenticationMethods.AnySafe);
    }

    [TestMethod]
    public void NetrcUsage_Members_HoldLibcurlsCurlNetrcValues()
    {
        Assert.AreEqual(0, (int)NetrcUsage.Ignored);
        Assert.AreEqual(1, (int)NetrcUsage.Optional);
        Assert.AreEqual(2, (int)NetrcUsage.Required);
    }

    [TestMethod]
    public void GssApiDelegation_Members_HoldLibcurlsCurlgssapiDelegationValues()
    {
        Assert.AreEqual(0, (int)GssApiDelegation.None);
        Assert.AreEqual(1, (int)GssApiDelegation.PolicyFlag);
        Assert.AreEqual(2, (int)GssApiDelegation.Flag);
    }

    [TestMethod]
    public void With_EachProperty_ChangesOnlyThatProperty()
    {
        AssertChangesOnly(Defaults with { Netrc = NetrcUsage.Required }, o => o with { Netrc = NetrcUsage.Ignored });
        AssertChangesOnly(Defaults with { NetrcFile = "netrc" }, o => o with { NetrcFile = null });
        AssertChangesOnly(Defaults with { UserPassword = "u:p" }, o => o with { UserPassword = null });
        AssertChangesOnly(Defaults with { UserName = "u" }, o => o with { UserName = null });
        AssertChangesOnly(Defaults with { Password = "p" }, o => o with { Password = null });
        AssertChangesOnly(Defaults with { LoginOptions = "AUTH=NTLM" }, o => o with { LoginOptions = null });
        AssertChangesOnly(Defaults with { HttpAuthentication = HttpAuthenticationMethods.Any }, o => o with { HttpAuthentication = HttpAuthenticationMethods.Basic });
        AssertChangesOnly(Defaults with { ProxyAuthentication = HttpAuthenticationMethods.AnySafe }, o => o with { ProxyAuthentication = HttpAuthenticationMethods.Basic });
        AssertChangesOnly(Defaults with { ProxyUserPassword = "pu:pp" }, o => o with { ProxyUserPassword = null });
        AssertChangesOnly(Defaults with { ProxyUserName = "pu" }, o => o with { ProxyUserName = null });
        AssertChangesOnly(Defaults with { ProxyPassword = "pp" }, o => o with { ProxyPassword = null });
        AssertChangesOnly(Defaults with { SaslAuthorizationIdentity = "admin" }, o => o with { SaslAuthorizationIdentity = null });
        AssertChangesOnly(Defaults with { SaslInitialResponse = true }, o => o with { SaslInitialResponse = false });
        AssertChangesOnly(Defaults with { XOAuth2Bearer = "token" }, o => o with { XOAuth2Bearer = null });
        AssertChangesOnly(Defaults with { DisallowUserNameInUrl = true }, o => o with { DisallowUserNameInUrl = false });
        AssertChangesOnly(Defaults with { UnrestrictedAuthentication = true }, o => o with { UnrestrictedAuthentication = false });
        AssertChangesOnly(Defaults with { ServiceName = "imap" }, o => o with { ServiceName = null });
        AssertChangesOnly(Defaults with { ProxyServiceName = "HTTP" }, o => o with { ProxyServiceName = null });
        AssertChangesOnly(Defaults with { GssApiDelegation = GssApiDelegation.Flag }, o => o with { GssApiDelegation = GssApiDelegation.None });
        AssertChangesOnly(Defaults with { KerberosLevel = "private" }, o => o with { KerberosLevel = null });
        AssertChangesOnly(Defaults with { AwsSigV4 = "aws:amz" }, o => o with { AwsSigV4 = null });
        AssertChangesOnly(Defaults with { TlsAuthType = "SRP" }, o => o with { TlsAuthType = null });
        AssertChangesOnly(Defaults with { TlsAuthUserName = "srpuser" }, o => o with { TlsAuthUserName = null });
        AssertChangesOnly(Defaults with { TlsAuthPassword = "srppass" }, o => o with { TlsAuthPassword = null });
        AssertChangesOnly(Defaults with { ProxyTlsAuthType = "SRP" }, o => o with { ProxyTlsAuthType = null });
        AssertChangesOnly(Defaults with { ProxyTlsAuthUserName = "psrpuser" }, o => o with { ProxyTlsAuthUserName = null });
        AssertChangesOnly(Defaults with { ProxyTlsAuthPassword = "psrppass" }, o => o with { ProxyTlsAuthPassword = null });
    }

    [TestMethod]
    public void ToString_WithEverySecretSet_PrintsNoneOfThem()
    {
        var options = Defaults with
        {
            UserPassword = "alice:SecretUserPwd",
            Password = "SecretPassword",
            ProxyUserPassword = "bob:SecretProxyUserPwd",
            ProxyPassword = "SecretProxyPassword",
            XOAuth2Bearer = "SecretBearerToken",
            TlsAuthPassword = "SecretTlsSrpPassword",
            ProxyTlsAuthPassword = "SecretProxyTlsSrpPassword",
        };

        var text = options.ToString();

        Assert.DoesNotContain("Secret", text);
        Assert.Contains("Password = <redacted>,", text);
        Assert.Contains("XOAuth2Bearer = <redacted>,", text);
        Assert.Contains("ProxyTlsAuthPassword = <redacted>", text);
    }

    [TestMethod]
    public void ToString_WithNoSecretSet_PrintsTheSecretsEmptyAndTheOtherMembers()
    {
        var text = (Defaults with { UserName = "alice", TlsAuthUserName = "srpuser" }).ToString();

        Assert.StartsWith("EasyAuthenticationOptions { Netrc = Ignored, ", text);
        Assert.Contains("UserName = alice,", text);
        Assert.Contains("TlsAuthUserName = srpuser,", text);
        Assert.Contains("Password = ,", text);
        Assert.DoesNotContain("<redacted>", text);
    }

    private static void AssertChangesOnly(EasyAuthenticationOptions changed, Func<EasyAuthenticationOptions, EasyAuthenticationOptions> restore)
    {
        Assert.AreNotEqual(Defaults, changed);
        Assert.AreEqual(Defaults, restore(changed));
    }
}
