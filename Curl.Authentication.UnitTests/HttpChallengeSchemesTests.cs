using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="HttpChallengeSchemes" /> to how curl 8.21.0 reads a challenge: split at
/// every comma, leading blanks skipped, a known scheme name in any case followed by a space
/// or the end.
/// </summary>
[TestClass]
public sealed class HttpChallengeSchemesTests
{
    public TestContext TestContext { get; set; } = null!;

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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge", challenge);
        diagnostics.Arrange("expected", expected);

        HttpAuthSchemes actual = HttpChallengeSchemes.Offered([challenge]);

        diagnostics.Act("offered", actual);
        diagnostics.Assert("schemes", expected, actual);
        Assert.AreEqual(expected, HttpChallengeSchemes.Offered([challenge]));
    }

    [TestMethod]
    public void Offered_SeveralValues_CombinesThem()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[] challenges = ["Digest realm=\"r\"", "Basic realm=\"r\""];
        diagnostics.Arrange("challenges", string.Join(" | ", challenges));

        HttpAuthSchemes actual = HttpChallengeSchemes.Offered(challenges);

        diagnostics.Act("offered", actual);
        diagnostics.Assert("schemes", HttpAuthSchemes.Digest | HttpAuthSchemes.Basic, actual);
        Assert.AreEqual(
            HttpAuthSchemes.Digest | HttpAuthSchemes.Basic,
            HttpChallengeSchemes.Offered(["Digest realm=\"r\"", "Basic realm=\"r\""]));
    }

    [TestMethod]
    public void Offered_NoValues_ReturnsNone()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenges", "(none)");

        HttpAuthSchemes actual = HttpChallengeSchemes.Offered([]);

        diagnostics.Act("offered", actual);
        diagnostics.Assert("schemes", HttpAuthSchemes.None, actual);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenges", string.Join(" | ", challenges));
        diagnostics.Arrange("expected", expected ?? "(null)");

        string? actual = HttpChallengeSchemes.NtlmTokenOf(challenges);

        diagnostics.Act("token", actual ?? "(null)");
        diagnostics.Diff("token", expected ?? "(null)", actual ?? "(null)");
        Assert.AreEqual(expected, HttpChallengeSchemes.NtlmTokenOf(challenges));
    }

    [TestMethod]
    [DataRow(new[] { "Negotiate" }, "", DisplayName = "Bare Negotiate")]
    [DataRow(new[] { "negotiate  oYG=  " }, "oYG=", DisplayName = "Any case, blanks around the token")]
    [DataRow(new[] { "NTLM, Negotiate oYG" }, "oYG", DisplayName = "After another challenge in one value")]
    [DataRow(new[] { "NTLM TlRM", "Basic realm=\"r\"" }, null, DisplayName = "No Negotiate challenge")]
    public void NegotiateTokenOf_Challenges_ReturnsTheFirstNegotiateToken(string[] challenges, string? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenges", string.Join(" | ", challenges));
        diagnostics.Arrange("expected", expected ?? "(null)");

        string? actual = HttpChallengeSchemes.NegotiateTokenOf(challenges);

        diagnostics.Act("token", actual ?? "(null)");
        diagnostics.Diff("token", expected ?? "(null)", actual ?? "(null)");
        Assert.AreEqual(expected, HttpChallengeSchemes.NegotiateTokenOf(challenges));
    }
}
