using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// BL-5037: where the engine matches curl's Schannel build, a Digest challenge with
/// <c>algorithm=MD5-sess</c> and no <c>qop</c> is answered, as curl 8.22.0 does, not refused with exit 94.
/// </summary>
[TestClass]
public sealed class DigestSspiChallengeMd5SessWithoutQopTests
{
    [TestMethod]
    public void CreateAuthorization_SspiBuildMd5SessWithoutQop_SendsAnAuthorizationHeader()
    {
        string? value = Answer("Digest realm=\"r\", nonce=\"n1\", algorithm=MD5-sess");

        Assert.IsNotNull(value);
        Assert.IsTrue(
            Regex.IsMatch(value, "^Digest username=\"user\",realm=\"r\",nonce=\"n1\",uri=\"/x\",algorithm=MD5-sess,response=\"[0-9a-f]{32}\"$"),
            value);
    }

    [TestMethod]
    public void CreateAuthorization_SspiBuildMd5SessWithQopAuth_StillCarriesTheClientNonce()
    {
        string? value = Answer("Digest realm=\"r\", nonce=\"n1\", qop=\"auth\", algorithm=MD5-sess");

        StringAssert.Contains(value, ",cnonce=\"c\",nc=00000001,algorithm=MD5-sess,", StringComparison.Ordinal);
    }

    [TestMethod]
    public void CreateAuthorization_SspiBuildMd5WithoutQop_StillAnswersWithoutAClientNonce()
    {
        string? value = Answer("Digest realm=\"r\", nonce=\"n1\", algorithm=MD5");

        StringAssert.Contains(value, ",algorithm=MD5,response=", StringComparison.Ordinal);
        Assert.DoesNotContain("cnonce", value!, StringComparison.Ordinal);
    }

    [TestMethod]
    public void CreateAuthorization_SspiBuildQopListWithoutAuth_StillFailsWithExit94()
    {
        HttpAuthenticationFailedException failure = Assert.ThrowsExactly<HttpAuthenticationFailedException>(
            () => Answer("Digest realm=\"r\", nonce=\"n1\", qop=\"auth-int\", algorithm=MD5-sess"));

        Assert.AreEqual(CurlExitCode.AuthError, failure.ExitCode);
    }

    private static string? Answer(string challenge) =>
        new DigestAuthenticator(Encoding.Latin1, () => "c", matchesSspiBuild: true).CreateAuthorization(
            new("GET", CurlUrl.Parse("http://127.0.0.1/x"), "/x", new NetworkCredential("user", "pass"), null, HttpAuthSchemes.Digest, IsProxy: false),
            [challenge]);
}
