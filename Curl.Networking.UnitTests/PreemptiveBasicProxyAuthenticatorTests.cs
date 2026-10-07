using System.Net;
using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void CreateAuthorization_BeforeAnyChallenge_AnswersBasic()
    {
        // curl -x http://127.0.0.1:18262 -U user:p@ss https://example.com:8443/path
        var authenticator = new PreemptiveBasicProxyAuthenticator(Encoding.UTF8);

        Diagnostics.Arrange("challenges", "none");

        var authorization = authenticator.CreateAuthorization(ConnectRequest, []);

        Diagnostics.Act("authorization", authorization);
        Diagnostics.Assert("authorization", "Basic dXNlcjpwQHNz", authorization);

        Assert.AreEqual("Basic dXNlcjpwQHNz", authorization);
    }

    [TestMethod]
    public void CreateAuthorization_EncodesTheCredentialWithTheGivenEncoding()
    {
        var authenticator = new PreemptiveBasicProxyAuthenticator(Encoding.Latin1);

        Diagnostics.Arrange("credential, encoding", "é:p, Latin-1");

        var authorization = authenticator.CreateAuthorization(ConnectRequest with { Credential = new NetworkCredential("é", "p") }, []);

        Diagnostics.Act("authorization", authorization);
        Diagnostics.Assert("authorization", "Basic 6Tpw", authorization);

        Assert.AreEqual("Basic 6Tpw", authorization);
    }

    [TestMethod]
    public void CreateAuthorization_AfterAChallenge_AnswersNothing()
    {
        var authenticator = new PreemptiveBasicProxyAuthenticator(Encoding.UTF8);

        Diagnostics.Arrange("challenges", "Basic realm=\"r\"");

        var authorization = authenticator.CreateAuthorization(ConnectRequest, ["Basic realm=\"r\""]);

        Diagnostics.Act("authorization", authorization);
        Diagnostics.Assert("authorization", null, authorization);

        Assert.IsNull(authorization);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Digest)]
    [DataRow(HttpAuthSchemes.Any)]
    public void CreateAuthorization_WhenBasicIsNotTheSchemeAllowed_AnswersNothing(HttpAuthSchemes allowed)
    {
        var authenticator = new PreemptiveBasicProxyAuthenticator(Encoding.UTF8);

        Diagnostics.Arrange("allowed schemes", allowed);

        var authorization = authenticator.CreateAuthorization(ConnectRequest with { AllowedSchemes = allowed }, []);

        Diagnostics.Act("authorization", authorization);
        Diagnostics.Assert("authorization", null, authorization);

        Assert.IsNull(authorization);
    }

    [TestMethod]
    public void CreateAuthorization_WithNoCredential_AnswersNothing()
    {
        var authenticator = new PreemptiveBasicProxyAuthenticator(Encoding.UTF8);

        Diagnostics.Arrange("credential", "none");

        var authorization = authenticator.CreateAuthorization(ConnectRequest with { Credential = null }, []);

        Diagnostics.Act("authorization", authorization);
        Diagnostics.Assert("authorization", null, authorization);

        Assert.IsNull(authorization);
    }
}
