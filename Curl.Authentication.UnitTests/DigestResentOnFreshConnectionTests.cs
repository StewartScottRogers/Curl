using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins the Digest answer curl 8.22.0's Schannel build sends again on a fresh connection when the
/// reused connection its first answer went out on died before the response (BL-3874, ADR-0950):
/// the same cnonce, <c>nc=00000002</c> and the hash for that count, which
/// <see cref="DigestAuthenticator.RepeatAuthorization" /> makes from the answer sent.
/// </summary>
[TestClass]
public sealed class DigestResentOnFreshConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    // Both values measured from curl 8.22.0 (Git for Windows' ucrt64) with --digest -u user:pass
    // against a 401 (realm "testrealm", nonce "abc123", qop "auth") then a 200 on a closed connection.
    [TestMethod]
    public void RepeatAuthorization_SspiAnswerSentOnADeadConnection_GivesTheMeasuredResend()
    {
        const string sent = "Digest username=\"user\",realm=\"testrealm\",nonce=\"abc123\",uri=\"/x\",cnonce=\"5d74647e52f26ab1028dfd2675a4e28e\","
            + "nc=00000001,response=\"a2687148fa88ec215c4f988d04dc7052\",qop=\"auth\"";
        var diagnostics = TestDiagnostics.For(TestContext);
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => throw new AssertFailedException("A resent answer keeps its cnonce."), matchesSspiBuild: true);
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://127.0.0.1:18874/x"), "/x", new NetworkCredential("user", "pass"), null, HttpAuthSchemes.Digest, IsProxy: false);
        diagnostics.Arrange("sent", sent);

        string value = authenticator.RepeatAuthorization(request, sent);

        const string expected = "Digest username=\"user\",realm=\"testrealm\",nonce=\"abc123\",uri=\"/x\",cnonce=\"5d74647e52f26ab1028dfd2675a4e28e\","
            + "nc=00000002,response=\"d811cfc2a8056c066b25e8140930d014\",qop=\"auth\"";
        diagnostics.Act("Authorization", value);
        diagnostics.Diff("Authorization", expected, value);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }
}
