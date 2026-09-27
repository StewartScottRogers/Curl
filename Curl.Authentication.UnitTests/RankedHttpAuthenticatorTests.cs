using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="RankedHttpAuthenticator" /> to the scheme curl 8.21.0 (mingw) answered
/// when a loopback server offered several (BL-218 Notes).
/// </summary>
[TestClass]
public sealed class RankedHttpAuthenticatorTests
{
    // The response curl sent for -u u:p, GET /a, realm "r", nonce "n", MD5, no qop.
    private const string MeasuredDigestResponse = "response=\"544c035f0f40d9ebf0295157d8041f8c\"";

    private const string Basic = "Basic realm=\"r\"";
    private const string Digest = "Digest realm=\"r\", nonce=\"n\"";

    private static readonly RankedHttpAuthenticator Authenticator = new(
        new BasicAndBearerAuthenticator(Encoding.UTF8),
        new DigestAuthenticator(Encoding.UTF8, () => "c"));

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, new[] { Basic, Digest }, DisplayName = "--anyauth, Basic then Digest")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Basic realm=\"r\", Digest realm=\"r\", nonce=\"n\"" }, DisplayName = "--anyauth, both in one header")]
    [DataRow(HttpAuthSchemes.Basic | HttpAuthSchemes.Digest, new[] { Basic, Digest }, DisplayName = "--basic --digest, Basic then Digest")]
    [DataRow(HttpAuthSchemes.Digest, new[] { Digest }, DisplayName = "--digest, Digest")]
    public void CreateAuthorization_DigestRanksFirst_AnswersDigest(HttpAuthSchemes allowed, string[] challenges)
    {
        string? value = Authenticator.CreateAuthorization(Request(allowed), challenges);

        StringAssert.StartsWith(value, "Digest username=\"u\"");
        StringAssert.Contains(value, MeasuredDigestResponse);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, DisplayName = "--anyauth, Basic only")]
    [DataRow(HttpAuthSchemes.Basic | HttpAuthSchemes.Digest, DisplayName = "--basic --digest, Basic only")]
    public void CreateAuthorization_OnlyBasicOfferedAndAllowed_AnswersBasic(HttpAuthSchemes allowed)
    {
        Assert.AreEqual("Basic dTpw", Authenticator.CreateAuthorization(Request(allowed), [Basic]));
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, new[] { Basic, "NTLM" }, DisplayName = "--anyauth, Basic and NTLM: NTLM picked")]
    [DataRow(HttpAuthSchemes.Any, new[] { "NTLM" }, DisplayName = "--anyauth, NTLM only")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Negotiate", Basic }, DisplayName = "--anyauth, Negotiate and Basic: Negotiate picked")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Negotiate", Digest }, DisplayName = "--anyauth, Negotiate and Digest: Negotiate picked")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Digest realm=\"r\"", Basic }, DisplayName = "--anyauth, Digest without nonce and Basic")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Foo realm=\"r\"" }, DisplayName = "--anyauth, unknown scheme only")]
    [DataRow(HttpAuthSchemes.Digest, new[] { Basic }, DisplayName = "--digest, Basic only")]
    [DataRow(HttpAuthSchemes.Basic, new[] { Digest }, DisplayName = "--basic, Digest only")]
    public void CreateAuthorization_PickIsNotAnswerable_SendsNothingAndDoesNotFallBack(HttpAuthSchemes allowed, string[] challenges)
    {
        Assert.IsNull(Authenticator.CreateAuthorization(Request(allowed), challenges));
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Basic, "Basic dTpw", DisplayName = "-u u:p")]
    [DataRow(HttpAuthSchemes.Any, null, DisplayName = "--anyauth")]
    [DataRow(HttpAuthSchemes.Digest, null, DisplayName = "--digest")]
    public void CreateAuthorization_BeforeAnyChallenge_AnswersAsCurlDoes(HttpAuthSchemes allowed, string? expected)
    {
        Assert.AreEqual(expected, Authenticator.CreateAuthorization(Request(allowed), []));
    }

    [TestMethod]
    public void CreateAuthorization_BearerOutranksDigest_AnswersBearer()
    {
        HttpAuthRequest request = Request(HttpAuthSchemes.Digest | HttpAuthSchemes.Bearer) with { BearerToken = "tok" };

        Assert.AreEqual("Bearer tok", Authenticator.CreateAuthorization(request, [Digest, "Bearer realm=\"r\""]));
    }

    [TestMethod]
    public void CreateAuthorization_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Authenticator.CreateAuthorization(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => Authenticator.CreateAuthorization(Request(HttpAuthSchemes.Any), null!));
    }

    private static HttpAuthRequest Request(HttpAuthSchemes allowed) =>
        new("GET", new Uri("http://127.0.0.1:18218/a"), "/a", new NetworkCredential("u", "p"), null, allowed, IsProxy: false);
}
