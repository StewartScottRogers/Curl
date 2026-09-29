using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="HttpChallengeSchemes" /> to how curl 8.21.0 reads a challenge: split at
/// every comma, leading blanks skipped, a known scheme name in any case followed by a space
/// or the end.
/// </summary>
[TestClass]
public sealed class HttpChallengeSchemesTests
{
    [TestMethod]
    [DataRow("Basic realm=\"r\"", HttpAuthSchemes.Basic, DisplayName = "Basic with a realm")]
    [DataRow("basic", HttpAuthSchemes.Basic, DisplayName = "Lower case, nothing after")]
    [DataRow("DIGEST\trealm=\"r\"", HttpAuthSchemes.Digest, DisplayName = "Upper case, tab")]
    [DataRow("NTLM", HttpAuthSchemes.Ntlm, DisplayName = "NTLM")]
    [DataRow("Negotiate abc=", HttpAuthSchemes.Negotiate, DisplayName = "Negotiate with a token68")]
    [DataRow("Bearer error=\"x\"", HttpAuthSchemes.Bearer, DisplayName = "Bearer")]
    [DataRow("  Basic, \t Bearer", HttpAuthSchemes.Basic | HttpAuthSchemes.Bearer, DisplayName = "Two in one value, blanks skipped")]
    [DataRow("Basic realm=\"a, Digest\"", HttpAuthSchemes.Basic, DisplayName = "A quoted comma splits, but a quote ends no scheme name")]
    [DataRow("Basic realm=\"a, Digest x\"", HttpAuthSchemes.Basic | HttpAuthSchemes.Digest, DisplayName = "A quoted comma splits, as in curl")]
    [DataRow("Basicx realm=\"r\"", HttpAuthSchemes.None, DisplayName = "Scheme name must end")]
    [DataRow("realm=\"r\"", HttpAuthSchemes.None, DisplayName = "An auth-param alone")]
    [DataRow("", HttpAuthSchemes.None, DisplayName = "Empty")]
    public void Offered_OneValue_ReturnsItsSchemes(string challenge, HttpAuthSchemes expected)
    {
        Assert.AreEqual(expected, HttpChallengeSchemes.Offered([challenge]));
    }

    [TestMethod]
    public void Offered_SeveralValues_CombinesThem()
    {
        Assert.AreEqual(
            HttpAuthSchemes.Digest | HttpAuthSchemes.Basic,
            HttpChallengeSchemes.Offered(["Digest realm=\"r\"", "Basic realm=\"r\""]));
    }

    [TestMethod]
    public void Offered_NoValues_ReturnsNone()
    {
        Assert.AreEqual(HttpAuthSchemes.None, HttpChallengeSchemes.Offered([]));
    }

    [TestMethod]
    [DataRow(new[] { "NTLM" }, "", DisplayName = "Bare NTLM")]
    [DataRow(new[] { "ntlm  TlRM=  " }, "TlRM=", DisplayName = "Any case, blanks around the token")]
    [DataRow(new[] { "Basic realm=\"r\", NTLM TlRM" }, "TlRM", DisplayName = "After another challenge in one value")]
    [DataRow(new[] { "Basic realm=\"r\"", "NTLM A", "NTLM B" }, "A", DisplayName = "The first of several")]
    [DataRow(new[] { "NTLMX abc", "Basic realm=\"r\"" }, null, DisplayName = "No NTLM challenge")]
    [DataRow(new string[0], null, DisplayName = "No challenges")]
    public void NtlmTokenOf_Challenges_ReturnsTheFirstNtlmToken(string[] challenges, string? expected)
    {
        Assert.AreEqual(expected, HttpChallengeSchemes.NtlmTokenOf(challenges));
    }
}
