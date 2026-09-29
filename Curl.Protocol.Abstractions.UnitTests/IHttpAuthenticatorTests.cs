namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the default members of <see cref="IHttpAuthenticator" />, which let an
/// authenticator that does no I/O (ADR-0176) or answers in one leg (ADR-0180) leave them out.
/// </summary>
[TestClass]
public sealed class IHttpAuthenticatorTests
{
    [TestMethod]
    public async Task CreateAuthorizationAsync_WhenNotOverridden_ReturnsTheSynchronousAnswer()
    {
        IHttpAuthenticator authenticator = new FixedAuthenticator();
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://example.com/"), "/", null, null, HttpAuthSchemes.Basic, IsProxy: false);

        string? authorization = await authenticator.CreateAuthorizationAsync(request, ["Basic"], CancellationToken.None);

        Assert.AreEqual("Fixed 1", authorization);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_WhenNotOverridden_AnswersNothing()
    {
        IHttpAuthenticator authenticator = new FixedAuthenticator();
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://example.com/"), "/", null, null, HttpAuthSchemes.Basic, IsProxy: false);

        string? authorization = await authenticator.ContinueAuthorizationAsync(request, "Basic dTpw", sentBeforeAnyChallenge: true, ["Basic"], CancellationToken.None);

        Assert.IsNull(authorization);
    }

    private sealed class FixedAuthenticator : IHttpAuthenticator
    {
        public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
            $"Fixed {challenges.Count}";
    }
}
