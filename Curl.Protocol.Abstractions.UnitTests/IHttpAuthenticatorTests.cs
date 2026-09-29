namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the default member of <see cref="IHttpAuthenticator" />, which lets an
/// authenticator that does no I/O leave it out (ADR-0176).
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

    private sealed class FixedAuthenticator : IHttpAuthenticator
    {
        public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
            $"Fixed {challenges.Count}";
    }
}
