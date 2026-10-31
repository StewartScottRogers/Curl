using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Libcurl.Options;

namespace Libcurl.UnitTests.Options.Authentication;

// Flips the process-wide EasyAuthenticationOptionSetters.SrpIsBuiltIn, so it must not run beside another test that reads it (BL-4020).
[TestClass]
[DoNotParallelize]
public sealed class EasyAuthenticationOptionSettersTests
{
    private bool srpWasBuiltIn;

    [TestInitialize]
    public void MakeSrpBuiltIn()
    {
        srpWasBuiltIn = EasyAuthenticationOptionSetters.SrpIsBuiltIn;
        EasyAuthenticationOptionSetters.SrpIsBuiltIn = true;
    }

    [TestCleanup]
    public void RestoreSrp() => EasyAuthenticationOptionSetters.SrpIsBuiltIn = srpWasBuiltIn;

    [TestMethod]
    public void SetOption_Netrc_TakesZeroToTwoAndRefusesTheRest()
    {
        Assert.AreEqual(NetrcUsage.Required, SetOk(CurlOption.Netrc, 2L).Netrc);
        Assert.AreEqual(NetrcUsage.Optional, SetOk(CurlOption.Netrc, NetrcUsage.Optional).Netrc);
        Assert.AreEqual(NetrcUsage.Ignored, SetOk(CurlOption.Netrc, 0).Netrc);
        Refused(CurlOption.Netrc, -1L);
        Refused(CurlOption.Netrc, 3L);
    }

    [TestMethod]
    public void SetOption_NetrcFile_SetsTheFileAndNullClearsIt() => Text(CurlOption.NetrcFile, a => a.NetrcFile);

    [TestMethod]
    public void SetOption_Userpwd_SetsTheCredentialsAndNullClearsThem() => Text(CurlOption.Userpwd, a => a.UserPassword);

    [TestMethod]
    public void SetOption_Username_SetsTheUserNameAndNullClearsIt() => Text(CurlOption.Username, a => a.UserName);

    [TestMethod]
    public void SetOption_Password_SetsThePasswordAndNullClearsIt() => Text(CurlOption.Password, a => a.Password);

    [TestMethod]
    public void SetOption_LoginOptions_SetsTheOptionsAndNullClearsThem() => Text(CurlOption.LoginOptions, a => a.LoginOptions);

    [TestMethod]
    public void SetOption_Proxyuserpwd_SetsTheCredentialsAndNullClearsThem() => Text(CurlOption.Proxyuserpwd, a => a.ProxyUserPassword);

    [TestMethod]
    public void SetOption_Proxyusername_SetsTheUserNameAndNullClearsIt() => Text(CurlOption.Proxyusername, a => a.ProxyUserName);

    [TestMethod]
    public void SetOption_Proxypassword_SetsThePasswordAndNullClearsIt() => Text(CurlOption.Proxypassword, a => a.ProxyPassword);

    [TestMethod]
    public void SetOption_SaslAuthzid_SetsTheIdentityAndNullClearsIt() => Text(CurlOption.SaslAuthzid, a => a.SaslAuthorizationIdentity);

    [TestMethod]
    public void SetOption_SaslIr_IsOnForNonZeroAndOffForZero() => Switch(CurlOption.SaslIr, a => a.SaslInitialResponse);

    [TestMethod]
    public void SetOption_Xoauth2Bearer_SetsTheTokenAndNullClearsIt() => Text(CurlOption.Xoauth2Bearer, a => a.XOAuth2Bearer);

    [TestMethod]
    public void SetOption_DisallowUsernameInUrl_IsOnForNonZeroAndOffForZero() => Switch(CurlOption.DisallowUsernameInUrl, a => a.DisallowUserNameInUrl);

    [TestMethod]
    public void SetOption_UnrestrictedAuth_IsOnForNonZeroAndOffForZero() => Switch(CurlOption.UnrestrictedAuth, a => a.UnrestrictedAuthentication);

    [TestMethod]
    public void SetOption_Httpauth_MapsTheCurlauthBitsToHttpAuthenticationMethods() =>
        AssertMapsMethods(CurlOption.Httpauth, a => a.HttpAuthentication);

    [TestMethod]
    public void SetOption_Proxyauth_MapsTheCurlauthBitsToHttpAuthenticationMethods() =>
        AssertMapsMethods(CurlOption.Proxyauth, a => a.ProxyAuthentication);

    [TestMethod]
    public void SetOption_Httpauth_RefusesABitBeyondThirtyTwoAndAnswersNotBuiltInForOnlyAlone()
    {
        Refused(CurlOption.Httpauth, 1L << 32);
        Refused(CurlOption.Proxyauth, (long)int.MinValue - 1);
        AssertNotBuiltIn(CurlOption.Httpauth, HttpAuthenticationMethods.Only);
        AssertNotBuiltIn(CurlOption.Proxyauth, 1L << 31);
    }

    [TestMethod]
    public void SetOption_ServiceName_SetsTheNameAndNullClearsIt() => Text(CurlOption.ServiceName, a => a.ServiceName);

    [TestMethod]
    public void SetOption_ProxyServiceName_SetsTheNameAndNullClearsIt() => Text(CurlOption.ProxyServiceName, a => a.ProxyServiceName);

    [TestMethod]
    public void SetOption_GssapiDelegation_KeepsOnlyTheTwoDelegationBits()
    {
        Assert.AreEqual(GssApiDelegation.Flag, SetOk(CurlOption.GssapiDelegation, 2L).GssApiDelegation);
        Assert.AreEqual(GssApiDelegation.PolicyFlag, SetOk(CurlOption.GssapiDelegation, 5L).GssApiDelegation);
        Assert.AreEqual(GssApiDelegation.PolicyFlag | GssApiDelegation.Flag, SetOk(CurlOption.GssapiDelegation, -1L).GssApiDelegation);
        Assert.AreEqual(GssApiDelegation.None, SetOk(CurlOption.GssapiDelegation, 4L).GssApiDelegation);
    }

    [TestMethod]
    public void SetOption_AwsSigv4_SetsTheProvidersAndTurnsOnSigV4AuthenticationAndNullClearsOnlyTheProviders()
    {
        var options = Set(new EasyOptions(), CurlOption.AwsSigv4, "aws:amz:us-east-1:s3");

        Assert.AreEqual("aws:amz:us-east-1:s3", options.Authentication.AwsSigV4);
        Assert.AreEqual(HttpAuthenticationMethods.AwsSigV4, options.Authentication.HttpAuthentication);

        var cleared = Set(options, CurlOption.AwsSigv4, null).Authentication;

        Assert.IsNull(cleared.AwsSigV4);
        Assert.AreEqual(HttpAuthenticationMethods.AwsSigV4, cleared.HttpAuthentication);
    }

    [TestMethod]
    public void SetOption_HttpsigAlgorithm_NamesEd25519OrHmacSha256AndZeroClearsItAndAnythingElseIsRefused()
    {
        Assert.AreEqual("ed25519", SetOk(CurlOption.HttpsigAlgorithm, 1L).HttpSignatureAlgorithm);
        Assert.AreEqual("hmac-sha256", SetOk(CurlOption.HttpsigAlgorithm, 2L).HttpSignatureAlgorithm);
        Assert.IsNull(Set(Set(new EasyOptions(), CurlOption.HttpsigAlgorithm, 1L), CurlOption.HttpsigAlgorithm, 0L).Authentication.HttpSignatureAlgorithm);
        Refused(CurlOption.HttpsigAlgorithm, 3L);
        Refused(CurlOption.HttpsigAlgorithm, -1L);
    }

    [TestMethod]
    public void SetOption_HttpsigKey_SetsTheKeyAndNullClearsIt() => Text(CurlOption.HttpsigKey, a => a.HttpSignatureKey);

    [TestMethod]
    public void SetOption_HttpsigKeyid_SetsTheKeyIdAndNullClearsIt() => Text(CurlOption.HttpsigKeyid, a => a.HttpSignatureKeyId);

    [TestMethod]
    public void SetOption_HttpsigHeaders_SetsTheHeadersAndNullClearsThem() => Text(CurlOption.HttpsigHeaders, a => a.HttpSignatureHeaders);
    [TestMethod]
    public void SetOption_TlsauthType_TakesSrpInAnyCaseOrNullAndRefusesAnythingElse() =>
        AssertTlsAuthType(CurlOption.TlsauthType, a => a.TlsAuthType);

    [TestMethod]
    public void SetOption_ProxyTlsauthType_TakesSrpInAnyCaseOrNullAndRefusesAnythingElse() =>
        AssertTlsAuthType(CurlOption.ProxyTlsauthType, a => a.ProxyTlsAuthType);

    [TestMethod]
    public void SetOption_TlsauthUsername_SetsTheUserNameAndNullClearsIt() => Text(CurlOption.TlsauthUsername, a => a.TlsAuthUserName);

    [TestMethod]
    public void SetOption_TlsauthPassword_SetsThePasswordAndNullClearsIt() => Text(CurlOption.TlsauthPassword, a => a.TlsAuthPassword);

    [TestMethod]
    public void SetOption_ProxyTlsauthUsername_SetsTheUserNameAndNullClearsIt() => Text(CurlOption.ProxyTlsauthUsername, a => a.ProxyTlsAuthUserName);

    [TestMethod]
    public void SetOption_ProxyTlsauthPassword_SetsThePasswordAndNullClearsIt() => Text(CurlOption.ProxyTlsauthPassword, a => a.ProxyTlsAuthPassword);

    private static void AssertMapsMethods(CurlOption option, Func<EasyAuthenticationOptions, HttpAuthenticationMethods> read)
    {
        Assert.AreEqual(HttpAuthenticationMethods.Digest | HttpAuthenticationMethods.Ntlm, read(SetOk(option, 10L)));
        Assert.AreEqual(HttpAuthenticationMethods.Any, read(SetOk(option, -17L)));
        Assert.AreEqual(HttpAuthenticationMethods.AnySafe, read(SetOk(option, HttpAuthenticationMethods.AnySafe)));
        Assert.AreEqual(HttpAuthenticationMethods.Only | HttpAuthenticationMethods.Bearer, read(SetOk(option, (1L << 31) | (1L << 6))));
        Assert.AreEqual(HttpAuthenticationMethods.None, read(SetOk(option, 0)));
    }

    private static void AssertTlsAuthType(CurlOption option, Func<EasyAuthenticationOptions, string?> read)
    {
        var options = Set(new EasyOptions(), option, "srp");

        Assert.AreEqual("srp", read(options.Authentication));
        Assert.AreEqual("SRP", read(SetOk(option, "SRP")));
        Assert.IsNull(read(Set(options, option, null).Authentication));
        Refused(option, "PSK");
    }

    private static void AssertNotBuiltIn(CurlOption option, object value)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.NotBuiltIn, EasyOptionDispatch.Default.SetOption(ref options, option, value), $"{option} {value}");
        Assert.AreSame(before, options);
    }

    private static EasyAuthenticationOptions SetOk(CurlOption option, object? value) => Set(new EasyOptions(), option, value).Authentication;

    private static EasyOptions Set(EasyOptions options, CurlOption option, object? value)
    {
        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, option, value), $"{option} {value}");
        return options;
    }

    private static void Refused(CurlOption option, object? value)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, EasyOptionDispatch.Default.SetOption(ref options, option, value), $"{option} {value}");
        Assert.AreSame(before, options);
    }

    private static void Switch(CurlOption option, Func<EasyAuthenticationOptions, bool> read)
    {
        Assert.IsTrue(read(SetOk(option, 2L)), $"{option} 2");
        Assert.IsFalse(read(SetOk(option, 0)), $"{option} 0");
    }

    private static void Text(CurlOption option, Func<EasyAuthenticationOptions, string?> read)
    {
        var options = Set(new EasyOptions(), option, "value");

        Assert.AreEqual("value", read(options.Authentication), $"{option}");
        Assert.IsNull(read(Set(options, option, null).Authentication), $"{option} null");
    }
}
