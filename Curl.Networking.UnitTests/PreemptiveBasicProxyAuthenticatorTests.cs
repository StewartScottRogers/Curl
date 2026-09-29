using System.Net;
using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the CONNECT's default <c>Proxy-Authorization</c>: Basic before any challenge, only
/// when Basic is the scheme allowed, and nothing after a challenge.
/// </summary>
[TestClass]
public sealed class PreemptiveBasicProxyAuthenticatorTests
{
    private static readonly HttpAuthRequest ConnectRequest = new(
        "CONNECT",
        CurlUrl.Parse("http://127.0.0.1:3128/"),
        "example.com:80",
        new NetworkCredential("user", "p@ss"),
        null,
        HttpAuthSchemes.Basic,
        IsProxy: true);

    [TestMethod]
    public void CreateAuthorization_BeforeAnyChallenge_AnswersBasic()
    {
        // curl -x http://127.0.0.1:18262 -U user:p@ss https://example.com:8443/path
        var authenticator = new PreemptiveBasicProxyAuthenticator(Encoding.UTF8);

        var authorization = authenticator.CreateAuthorization(ConnectRequest, []);

        Assert.AreEqual("Basic dXNlcjpwQHNz", authorization);
    }

    [TestMethod]
    public void CreateAuthorization_EncodesTheCredentialWithTheGivenEncoding()
    {
        var authenticator = new PreemptiveBasicProxyAuthenticator(Encoding.Latin1);

        var authorization = authenticator.CreateAuthorization(ConnectRequest with { Credential = new NetworkCredential("é", "p") }, []);

        Assert.AreEqual("Basic 6Tpw", authorization);
    }

    [TestMethod]
    public void CreateAuthorization_AfterAChallenge_AnswersNothing()
    {
        var authenticator = new PreemptiveBasicProxyAuthenticator(Encoding.UTF8);

        var authorization = authenticator.CreateAuthorization(ConnectRequest, ["Basic realm=\"r\""]);

        Assert.IsNull(authorization);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Digest)]
    [DataRow(HttpAuthSchemes.Any)]
    public void CreateAuthorization_WhenBasicIsNotTheSchemeAllowed_AnswersNothing(HttpAuthSchemes allowed)
    {
        var authenticator = new PreemptiveBasicProxyAuthenticator(Encoding.UTF8);

        var authorization = authenticator.CreateAuthorization(ConnectRequest with { AllowedSchemes = allowed }, []);

        Assert.IsNull(authorization);
    }

    [TestMethod]
    public void CreateAuthorization_WithNoCredential_AnswersNothing()
    {
        var authenticator = new PreemptiveBasicProxyAuthenticator(Encoding.UTF8);

        var authorization = authenticator.CreateAuthorization(ConnectRequest with { Credential = null }, []);

        Assert.IsNull(authorization);
    }
}
