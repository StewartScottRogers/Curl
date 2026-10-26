using System.Net;
using System.Text;

using Curl.Authentication;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins CGR-054 (BL-3583): curl 8.22.0 signs the path it sends, with its <c>%XX</c> escapes in upper
/// case. The signature is the one it sent for <c>-sS --aws-sigv4 aws:amz:us-east-1:s3 -u AKID:SECRET
/// -H "X-Amz-Date: 20250101T000000Z" http://127.0.0.1:20583/a%2fb%c3%bc</c> (measured, BL-3583 Notes).
/// </summary>
[TestClass]
public sealed class Cgr054Tests
{
    [TestMethod]
    public async Task CreateAuthorizationAsync_LowerCaseEscapesInThePath_SignsThemUpperCased()
    {
        CurlUrl url = CurlUrl.Parse("http://127.0.0.1:20583/a%2fb%c3%bc");
        HttpAuthRequest request = new("GET", url, "/a%2Fb%C3%BC", new NetworkCredential("AKID", "SECRET"), null, HttpAuthSchemes.Basic, IsProxy: false)
        {
            AwsSigV4 = new AwsSigV4Inputs("aws:amz:us-east-1:s3", "127.0.0.1:20583", ["X-Amz-Date: 20250101T000000Z"]) { IsGetOrHead = true },
        };
        AwsSigV4HttpAuthenticator authenticator = new(new NoOtherSchemes(), new AwsSigV4Signer(new FixedUtcClock(new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)), Encoding.Latin1));

        string? value = await authenticator.CreateAuthorizationAsync(request, [], CancellationToken.None);

        Assert.Contains("Signature=c12822d857213145443eee04286c029d0057b90da2e0abf9acfd15b23cc00332", value!);
    }

    private sealed class FixedUtcClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Stands for the other schemes, which a signed request never reaches.</summary>
    private sealed class NoOtherSchemes : IHttpAuthenticator
    {
        public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) => null;

        public ValueTask<string?> CreateAuthorizationAsync(HttpAuthRequest request, IReadOnlyList<string> challenges, CancellationToken cancellationToken) =>
            ValueTask.FromResult<string?>(null);

        public ValueTask<string?> ContinueAuthorizationAsync(HttpAuthRequest request, string sentAuthorization, bool sentBeforeAnyChallenge, IReadOnlyList<string> challenges, CancellationToken cancellationToken) =>
            ValueTask.FromResult<string?>(null);

        public string RepeatAuthorization(HttpAuthRequest request, string sentAuthorization) => sentAuthorization;

        public void EndAuthorization(string sentAuthorization)
        {
        }
    }
}
