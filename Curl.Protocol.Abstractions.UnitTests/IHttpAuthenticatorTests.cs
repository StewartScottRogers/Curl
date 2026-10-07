using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the default members of <see cref="IHttpAuthenticator" />, which let an
/// authenticator that does no I/O (ADR-0176) or answers in one leg (ADR-0181) leave them out.
/// </summary>
[TestClass]
public sealed class IHttpAuthenticatorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CreateAuthorizationAsync_WhenNotOverridden_ReturnsTheSynchronousAnswer()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IHttpAuthenticator authenticator = new FixedAuthenticator();
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://example.com/"), "/", null, null, HttpAuthSchemes.Basic, IsProxy: false);
        diagnostics.Arrange("request", request);
        diagnostics.Arrange("challenges", "Basic");

        string? authorization = await authenticator.CreateAuthorizationAsync(request, ["Basic"], CancellationToken.None);

        diagnostics.Act("authorization", authorization);
        diagnostics.Diff("authorization", "Fixed 1", authorization ?? string.Empty);
        Assert.AreEqual("Fixed 1", authorization);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_WhenNotOverridden_AnswersNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IHttpAuthenticator authenticator = new FixedAuthenticator();
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://example.com/"), "/", null, null, HttpAuthSchemes.Basic, IsProxy: false);
        diagnostics.Arrange("request", request);
        diagnostics.Arrange("sent authorization", "Basic dTpw");

        string? authorization = await authenticator.ContinueAuthorizationAsync(request, "Basic dTpw", sentBeforeAnyChallenge: true, ["Basic"], CancellationToken.None);

        diagnostics.Act("authorization", authorization);
        diagnostics.Assert("authorization", null, authorization);
        Assert.IsNull(authorization);
    }

    [TestMethod]
    public void RepeatAuthorization_WhenNotOverridden_SendsTheValueAsSent()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IHttpAuthenticator authenticator = new FixedAuthenticator();
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://example.com/"), "/", null, null, HttpAuthSchemes.Basic, IsProxy: false);
        diagnostics.Arrange("request", request);
        diagnostics.Arrange("sent authorization", "Basic dTpw");

        string authorization = authenticator.RepeatAuthorization(request, "Basic dTpw");

        diagnostics.Act("authorization", authorization);
        diagnostics.Diff("authorization", "Basic dTpw", authorization);
        Assert.AreEqual("Basic dTpw", authorization);
    }

    [TestMethod]
    public void EndAuthorization_WhenNotOverridden_DoesNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IHttpAuthenticator authenticator = new FixedAuthenticator();
        diagnostics.Arrange("ended authorization", "Negotiate YQ==");

        authenticator.EndAuthorization("Negotiate YQ==");

        string? authorization = authenticator.CreateAuthorization(new("GET", CurlUrl.Parse("http://example.com/"), "/", null, null, HttpAuthSchemes.Basic, IsProxy: false), []);
        diagnostics.Act("authorization after end", authorization);
        diagnostics.Diff("authorization after end", "Fixed 0", authorization ?? string.Empty);
        Assert.AreEqual("Fixed 0", authenticator.CreateAuthorization(new("GET", CurlUrl.Parse("http://example.com/"), "/", null, null, HttpAuthSchemes.Basic, IsProxy: false), []));
    }

    private sealed class FixedAuthenticator : IHttpAuthenticator
    {
        public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
            $"Fixed {challenges.Count}";
    }
}
