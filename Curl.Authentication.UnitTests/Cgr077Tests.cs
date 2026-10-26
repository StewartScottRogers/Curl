using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// CGR-077: where the engine matches curl's Schannel build, a Digest challenge is answered as
/// the SSPI behind curl 8.22.0's Windows build answers it - the challenges it refuses with exit
/// 94, the user names it refuses, the realm taken from a <c>DOMAIN\user</c> name, and the
/// values it escapes or sends as they are (measured 2026-10-26 with Record-CurlExchange.ps1,
/// BL-3606).
/// </summary>
[TestClass]
public sealed class Cgr077Tests
{
    private const string Target = "/p?x=1";

    private const string Challenge = "Digest realm=\"testrealm\", nonce=\"abc123\", qop=\"auth\"";

    private const string Answered = "Digest username=\"user\",realm=\"testrealm\",nonce=\"abc123\",uri=\"/p?x=1\",cnonce=\"c\",nc=00000001,response=R,qop=\"auth\"";

    [TestMethod]
    [DataRow("Digest realm=\"testrealm\", nonce=\"abc123\", algorithm=FOO", DisplayName = "unknown algorithm")]
    [DataRow("Digest realm=\"a\", nonce=\"abc123\", algorithm=SHA-256", DisplayName = "SHA-256")]
    [DataRow("Digest realm=\"testrealm\", nonce=\"abc123\", qop=\"auth-int\"", DisplayName = "auth-int only")]
    [DataRow("Digest realm=\"testrealm\", nonce=\"abc123\", qop=\"foo\"", DisplayName = "unknown qop")]
    [DataRow("Digest realm=\"testrealm\", qop=\"auth\"", DisplayName = "no nonce")]
    [DataRow("Digest realm=\"testrealm\", nonce=\"abc123\", qop=\"auth\",", DisplayName = "trailing comma")]
    [DataRow("Digest realm=\"testrealm\", algorithm=MD5, algorithm=MD5, nonce=\"1\", nonce=\"2\"", DisplayName = "two nonces")]
    [DataRow("Digest realm=\"a\", , nonce=\"abc123\"", DisplayName = "empty element")]
    [DataRow("Digest realm=\"a\", nonce=\"abc123\", foo", DisplayName = "key without value")]
    [DataRow("Digest realm=\"a\", nonce=\"abc123\", algorithm=MD5-sess", DisplayName = "MD5-sess without qop")]
    [DataRow("Digest realm=\"a\", nonce=\"abc", DisplayName = "unclosed quote")]
    [DataRow("Digest realm=\"a\", nonce=\"abc\\", DisplayName = "unclosed quote ending in a backslash")]
    [DataRow("Digest realm=\"a\", nonce=\"abc\"x", DisplayName = "text after a quoted value")]
    [DataRow("Digest", DisplayName = "no parameters")]
    public void CreateAuthorization_ChallengeSspiRefuses_FailsWithExit94(string challenge)
    {
        HttpAuthenticationFailedException failure = Assert.ThrowsExactly<HttpAuthenticationFailedException>(
            () => Answer("user", challenge));

        Assert.AreEqual(CurlExitCode.AuthError, failure.ExitCode);
        Assert.AreEqual("An authentication function returned an error", failure.Message);
    }

    [TestMethod]
    [DataRow("hello\n\r", CurlExitCode.AuthError, "An authentication function returned an error", DisplayName = "CR and LF")]
    [DataRow("us\tr", CurlExitCode.AuthError, "An authentication function returned an error", DisplayName = "tab")]
    [DataRow("", CurlExitCode.LoginDenied, "Login denied", DisplayName = "empty")]
    public void CreateAuthorization_UserNameSspiRefuses_FailsAsCurl(string user, CurlExitCode exitCode, string message)
    {
        HttpAuthenticationFailedException failure = Assert.ThrowsExactly<HttpAuthenticationFailedException>(
            () => Answer(user, Challenge));

        Assert.AreEqual(exitCode, failure.ExitCode);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    [DataRow("user", Challenge + ", userhash=true", Answered, DisplayName = "userhash ignored")]
    [DataRow("user", "Digest realm=\"te\\\"st\", nonce=\"a\\\\bc\", qop=\"auth\"", "Digest username=\"user\",realm=\"te\\\"st\",nonce=\"a\\bc\",uri=\"/p?x=1\",cnonce=\"c\",nc=00000001,response=R,qop=\"auth\"", DisplayName = "realm escaped, nonce as it is")]
    [DataRow("user", "Digest realm=\"a\", nonce=\"a\\\"b\", opaque=\"o\\\"p\"", "Digest username=\"user\",realm=\"a\",nonce=\"a\"b\",uri=\"/p?x=1\",response=R,opaque=\"o\"p\"", DisplayName = "nonce and opaque as they are")]
    [DataRow("user", "DIGEST REALM=\"testrealm\", NONCE=\"abc123\", ALGORITHM=md5, QOP=\"auth\"", "Digest username=\"user\",realm=\"testrealm\",nonce=\"abc123\",uri=\"/p?x=1\",cnonce=\"c\",nc=00000001,algorithm=MD5,response=R,qop=\"auth\"", DisplayName = "upper case")]
    [DataRow("user", "Digest realm=\"a\", nonce=\"abc123\", algorithm=md5-sess, qop=\"auth\"", "Digest username=\"user\",realm=\"a\",nonce=\"abc123\",uri=\"/p?x=1\",cnonce=\"c\",nc=00000001,algorithm=MD5-sess,response=R,qop=\"auth\"", DisplayName = "MD5-sess")]
    [DataRow("user", "Digest realm=\"a\", charset=UTF-8, nonce=\"abc123\", opaque=\"op\", qop=\"auth\"", "Digest username=\"user\",realm=\"a\",nonce=\"abc123\",uri=\"/p?x=1\",cnonce=\"c\",nc=00000001,response=R,qop=\"auth\",opaque=\"op\",charset=utf-8", DisplayName = "charset")]
    [DataRow("user", "Digest realm=\"a\", nonce=\"abc123\", charset=ISO-8859-1", "Digest username=\"user\",realm=\"a\",nonce=\"abc123\",uri=\"/p?x=1\",response=R", DisplayName = "other charset")]
    [DataRow("user", "Digest    realm = \"testrealm\" ,  nonce = \"abc123\" ,qop=\"auth\"", "Digest username=\"user\",realm=\"\",nonce=\"abc123\",uri=\"/p?x=1\",cnonce=\"c\",nc=00000001,response=R,qop=\"auth\"", DisplayName = "blanks around =")]
    [DataRow("user", "Digest realm=a, nonce=abc123 , qop=auth, opaque=op", "Digest username=\"user\",realm=\"a\",nonce=\"abc123\",uri=\"/p?x=1\",cnonce=\"c\",nc=00000001,response=R,qop=\"auth\",opaque=\"op\"", DisplayName = "unquoted")]
    [DataRow("user", "Digest realm=\"a\", realm=\"b\", nonce=\"abc123\", qop=\"auth\", qop=\"auth\"", "Digest username=\"user\",realm=\"b\",nonce=\"abc123\",uri=\"/p?x=1\",cnonce=\"c\",nc=00000001,response=R,qop=\"auth\"", DisplayName = "repeated realm")]
    [DataRow("user", "Digest\"k\"=v,nonce=\"n\"", "Digest username=\"user\",realm=\"\",nonce=\"n\",uri=\"/p?x=1\",response=R", DisplayName = "no blank after Digest")]
    [DataRow("dom\\user", Challenge, "Digest username=\"user\",realm=\"dom\",nonce=\"abc123\",uri=\"/p?x=1\",cnonce=\"c\",nc=00000001,response=R,qop=\"auth\"", DisplayName = "DOMAIN\\user")]
    [DataRow("dom/user", Challenge, "Digest username=\"user\",realm=\"dom\",nonce=\"abc123\",uri=\"/p?x=1\",cnonce=\"c\",nc=00000001,response=R,qop=\"auth\"", DisplayName = "DOMAIN/user")]
    [DataRow("dom\\", Challenge, "Digest username=\"\",realm=\"dom\",nonce=\"abc123\",uri=\"/p?x=1\",cnonce=\"c\",nc=00000001,response=R,qop=\"auth\"", DisplayName = "domain only")]
    [DataRow("\\user", Challenge, Answered, DisplayName = "empty domain")]
    [DataRow("us\u00e9r", Challenge, "Digest username=\"us\u00e9r\",realm=\"testrealm\",nonce=\"abc123\",uri=\"/p?x=1\",cnonce=\"c\",nc=00000001,response=R,qop=\"auth\"", DisplayName = "non-ASCII sent raw")]
    [DataRow("u\"s", Challenge, "Digest username=\"u\\\"s\",realm=\"testrealm\",nonce=\"abc123\",uri=\"/p?x=1\",cnonce=\"c\",nc=00000001,response=R,qop=\"auth\"", DisplayName = "quote in user escaped")]
    public void CreateAuthorization_SspiBuild_AnswersAsCurl(string user, string challenge, string expected)
    {
        string? value = Answer(user, challenge);

        Assert.AreEqual(expected, Regex.Replace(value!, "response=\"[0-9a-f]{32}\"", "response=R"));
    }

    [TestMethod]
    public void CreateAuthorization_SspiBuildLongNonce_KeepsTheQop()
    {
        string nonce = new('n', 3000);

        string? value = Answer("user", "Digest realm=\"testrealm\", nonce=\"" + nonce + "\", qop=\"auth\"");

        StringAssert.EndsWith(value, ",qop=\"auth\"", StringComparison.Ordinal);
        StringAssert.Contains(value, "nonce=\"" + nonce + "\",uri=\"/p?x=1\",cnonce=\"c\",nc=00000001,", StringComparison.Ordinal);
    }

    // upstream case 1229's URL user name; response as curl 8.22.0 (Schannel) sent it.
    [TestMethod]
    public void CreateAuthorization_SspiBuildBackslashUserAndQuotedTarget_MatchesTheMeasuredAnswer()
    {
        DigestAuthenticator authenticator = new(Encoding.Latin1, () => "c", matchesSspiBuild: true);
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://127.0.0.1/1229"), "/1229", new NetworkCredential("\\user\"", "password"), null, HttpAuthSchemes.Digest, IsProxy: false);

        string? value = authenticator.CreateAuthorization(request, ["Digest realm=\"testrealm\", nonce=\"1053604145\""]);

        Assert.AreEqual("Digest username=\"user\\\"\",realm=\"testrealm\",nonce=\"1053604145\",uri=\"/1229\",response=\"ed551ba287723feffb063cafe9c43daa\"", value);
    }

    [TestMethod]
    public void CreateAuthorization_SspiBuildQuoteInTarget_SendsTheUriAsItIs()
    {
        DigestAuthenticator authenticator = new(Encoding.Latin1, () => "c", matchesSspiBuild: true);
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://127.0.0.1/64"), "/64\"", new NetworkCredential("testuser", "testpass"), null, HttpAuthSchemes.Digest, IsProxy: false);

        string? value = authenticator.CreateAuthorization(request, ["Digest realm=\"testrealm\", nonce=\"1053604145\""]);

        StringAssert.Contains(value, ",uri=\"/64\"\",response=", StringComparison.Ordinal);
    }

    [TestMethod]
    public void CreateAuthorization_SspiBuildNoDigestChallenge_ReturnsNull()
    {
        string? value = Answer("user", "Basic realm=\"testrealm\"");

        Assert.IsNull(value);
    }

    [TestMethod]
    public void RepeatAuthorization_SspiBuildDomainUser_CountsOnForTheUserPart()
    {
        DigestAuthenticator authenticator = new(Encoding.Latin1, () => "c", matchesSspiBuild: true);
        HttpAuthRequest request = Request("dom\\user");
        string first = authenticator.CreateAuthorization(request, [Challenge])!;

        string second = authenticator.RepeatAuthorization(request, first);

        StringAssert.StartsWith(second, "Digest username=\"user\",realm=\"dom\",", StringComparison.Ordinal);
        StringAssert.Contains(second, ",nc=00000002,", StringComparison.Ordinal);
    }

    private static string? Answer(string user, string challenge) =>
        new DigestAuthenticator(Encoding.Latin1, () => "c", matchesSspiBuild: true).CreateAuthorization(Request(user), [challenge]);

    private static HttpAuthRequest Request(string user) =>
        new("GET", CurlUrl.Parse("http://127.0.0.1" + Target), Target, new NetworkCredential(user, "pass"), null, HttpAuthSchemes.Digest, IsProxy: false);
}
