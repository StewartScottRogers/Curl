using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="BasicAndBearerAuthenticator" /> to the Authorization values curl 8.21.0
/// (mingw, Windows-1252) sent to a loopback server. Each case was measured (BL-216 Notes).
/// </summary>
[TestClass]
public sealed class BasicAndBearerAuthenticatorTests
{
    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    private static readonly BasicAndBearerAuthenticator Authenticator = new(Windows1252);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("u", "p", "Basic dTpw", DisplayName = "-u u:p")]
    [DataRow("user", "", "Basic dXNlcjo=", DisplayName = "-u user:")]
    [DataRow("", "p", "Basic OnA=", DisplayName = "-u :p")]
    [DataRow("u", "p:q", "Basic dTpwOnE=", DisplayName = "-u u:p:q")]
    [DataRow("é", "p€", "Basic 6TpwgA==", DisplayName = "Non-ASCII in Windows-1252: bytes E9 3A 70 80")]
    [DataRow("Ω中Ā", "p", "Basic Tz9BOnA=", DisplayName = "Unmappable characters best-fitted: O?A")]
    public void CreateAuthorization_BasicBeforeAnyResponse_MatchesCurl(string user, string password, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", user);
        diagnostics.Arrange("password", password);
        diagnostics.Arrange("allowed schemes", HttpAuthSchemes.Basic);

        string? value = Authenticator.CreateAuthorization(Request(new NetworkCredential(user, password), null, HttpAuthSchemes.Basic), []);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    [DataRow("tok", "Bearer tok", DisplayName = "--oauth2-bearer tok")]
    [DataRow("té", "Bearer té", DisplayName = "Non-ASCII token: byte E9")]
    [DataRow("t€", "Bearer t\u0080", DisplayName = "Euro sign in the token: byte 80")]
    public void CreateAuthorization_BearerBeforeAnyResponse_MatchesCurlBytes(string token, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("token", token);
        diagnostics.Arrange("allowed schemes", HttpAuthSchemes.Bearer);

        string? value = Authenticator.CreateAuthorization(Request(null, token, HttpAuthSchemes.Bearer), []);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    public void CreateAuthorization_UserAndBearerToken_SendsBearer()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", "u:p");
        diagnostics.Arrange("token", "tok");
        diagnostics.Arrange("allowed schemes", HttpAuthSchemes.Bearer);

        string? value = Authenticator.CreateAuthorization(
            Request(new NetworkCredential("u", "p"), "tok", HttpAuthSchemes.Bearer), []);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", "Bearer tok", value);
        Assert.AreEqual("Bearer tok", value);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, DisplayName = "--anyauth")]
    [DataRow(HttpAuthSchemes.Basic | HttpAuthSchemes.Digest, DisplayName = "--basic --digest")]
    [DataRow(HttpAuthSchemes.Basic | HttpAuthSchemes.Bearer, DisplayName = "Basic and Bearer")]
    [DataRow(HttpAuthSchemes.Digest, DisplayName = "--digest")]
    [DataRow(HttpAuthSchemes.None, DisplayName = "No scheme")]
    public void CreateAuthorization_NotExactlyBasicOrBearerBeforeAnyResponse_SendsNothing(HttpAuthSchemes allowed)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", "u:p");
        diagnostics.Arrange("token", "tok");
        diagnostics.Arrange("allowed schemes", allowed);

        string? value = Authenticator.CreateAuthorization(Request(new NetworkCredential("u", "p"), "tok", allowed), []);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", null, value);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void CreateAuthorization_BasicWithoutCredential_SendsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", null);
        diagnostics.Arrange("allowed schemes", HttpAuthSchemes.Basic);

        string? value = Authenticator.CreateAuthorization(Request(null, null, HttpAuthSchemes.Basic), []);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", null, value);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void CreateAuthorization_BearerWithoutToken_SendsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("token", null);
        diagnostics.Arrange("allowed schemes", HttpAuthSchemes.Bearer);

        string? value = Authenticator.CreateAuthorization(Request(null, null, HttpAuthSchemes.Bearer), []);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", null, value);
        Assert.IsNull(value);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, "Basic realm=\"r\"", "Basic dTpw", DisplayName = "--anyauth, Basic challenge")]
    [DataRow(HttpAuthSchemes.Basic | HttpAuthSchemes.Digest, "Basic realm=\"r\"", "Basic dTpw", DisplayName = "--basic --digest, Basic challenge")]
    [DataRow(HttpAuthSchemes.Any | HttpAuthSchemes.Bearer, "Bearer realm=\"r\"", "Bearer tok", DisplayName = "--anyauth with a token, Bearer challenge")]
    [DataRow(HttpAuthSchemes.Any | HttpAuthSchemes.Bearer, "Basic realm=\"r\", Bearer", "Bearer tok", DisplayName = "Bearer outranks Basic")]
    public void CreateAuthorization_AllowedSchemeChallenged_AnswersIt(HttpAuthSchemes allowed, string challenge, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("allowed schemes", allowed);
        diagnostics.Arrange("challenge", challenge);

        string? value = Authenticator.CreateAuthorization(Request(new NetworkCredential("u", "p"), "tok", allowed), [challenge]);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Digest, "Basic realm=\"r\"", DisplayName = "--digest, Basic challenge")]
    [DataRow(HttpAuthSchemes.Any, "Digest realm=\"r\", Basic realm=\"r\"", DisplayName = "Digest outranks Basic and is not built here")]
    [DataRow(HttpAuthSchemes.Any, "Negotiate", DisplayName = "Negotiate only")]
    [DataRow(HttpAuthSchemes.Any, "Custom realm=\"r\"", DisplayName = "Unknown scheme")]
    public void CreateAuthorization_NoBasicOrBearerPicked_SendsNothing(HttpAuthSchemes allowed, string challenge)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("allowed schemes", allowed);
        diagnostics.Arrange("challenge", challenge);

        string? value = Authenticator.CreateAuthorization(Request(new NetworkCredential("u", "p"), "tok", allowed), [challenge]);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", null, value);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void CreateAuthorization_ProxyBasic_AnswersWithTheProxyCredential()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        HttpAuthRequest request = Request(new NetworkCredential("u", "p"), null, HttpAuthSchemes.Basic) with { IsProxy = true };
        diagnostics.Arrange("credential", "u:p");
        diagnostics.Arrange("is proxy", request.IsProxy);

        string? value = Authenticator.CreateAuthorization(request, []);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", "Basic dTpw", value);
        Assert.AreEqual("Basic dTpw", value);
    }

    private static HttpAuthRequest Request(NetworkCredential? credential, string? token, HttpAuthSchemes allowed) =>
        new("GET", CurlUrl.Parse("http://127.0.0.1/"), "/", credential, token, allowed, IsProxy: false);
}
