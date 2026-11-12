using Curl.Extended.Protocol.OciRegistry.Auth;
using Curl.Extended.Protocol.OciRegistry.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.OciRegistry;

/// <summary>
/// Pins that <c>-u</c>'s credentials never leave the HTTPS registry for a plain <c>http</c> token realm (BL-4901), the
/// downgrade curl refuses on a redirect (CVE-2022-27774), while an <c>https</c> realm still gets them.
/// </summary>
[TestClass]
public sealed class OciTokenRealmSchemeTests
{
    [TestMethod]
    public void Build_CredentialsForAnHttpRealm_ThrowsNamingTheRealm()
    {
        var challenge = new OciBearerChallenge("http://127.0.0.1:18701/token", "reg", "repository:httprealm:pull");

        var failure = Assert.ThrowsExactly<OciTransferException>(() => OciTokenRequestBuilder.Build(challenge, "user:pass"));

        Assert.AreEqual(CurlExitCode.LoginDenied, failure.ExitCode);
        Assert.AreEqual("Refusing to send credentials to the non-HTTPS token realm http://127.0.0.1:18701/token.", failure.Message);
    }

    [TestMethod]
    public void Build_CredentialsForAnHttpsRealm_SendsBasicCredentials()
    {
        var challenge = new OciBearerChallenge("https://127.0.0.1:18701/token", "reg", "repository:httprealm:pull");

        var request = OciTokenRequestBuilder.Build(challenge, "user:pass");

        Assert.AreEqual("https://127.0.0.1:18701/token?service=reg&scope=repository%3Ahttprealm%3Apull", request?.Url.AbsoluteUri);
        Assert.AreEqual(new KeyValuePair<string, string>("Authorization", "Basic dXNlcjpwYXNz"), request?.Headers[1]);
    }

    [TestMethod]
    public void Build_AnonymousForAnHttpRealm_SendsNoCredentials()
    {
        var challenge = new OciBearerChallenge("http://127.0.0.1:18701/token", "reg", null);

        var request = OciTokenRequestBuilder.Build(challenge, null);

        Assert.AreEqual(1, request?.Headers.Count);
    }
}
