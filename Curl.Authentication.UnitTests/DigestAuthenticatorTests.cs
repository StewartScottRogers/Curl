using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="DigestAuthenticator" /> to the RFC 7616 section 3.9 examples and to the
/// Authorization values curl 8.21.0 (the OpenSSL build, <c>curlimages/curl:8.21.0</c>)
/// sent to a loopback server, each reproduced with its cnonce injected (BL-217 Notes).
/// </summary>
[TestClass]
public sealed class DigestAuthenticatorTests
{
    private const string MeasuredTarget = "/dir/index.html?x=1";

    public TestContext TestContext { get; set; } = null!;

    // The RFC prints the MD5 response with a typo (...eebdec3); this is the value its errata
    // and an independent computation (Python's hashlib) give.
    [TestMethod]
    [DataRow("MD5", "8ca523f5e9506fed4657c9700eebdbec", DisplayName = "RFC 7616 3.9.1, MD5")]
    [DataRow("SHA-256", "753927fa0e85d155564e2e272a28d1802ca10daf4496794697cf8db5856cb6c1", DisplayName = "RFC 7616 3.9.1, SHA-256")]
    public void CreateAuthorization_Rfc7616Section391_GivesTheExampleResponse(string algorithm, string response)
    {
        string challenge = "Digest realm=\"http-auth@example.org\", qop=\"auth, auth-int\", algorithm=" + algorithm
            + ", nonce=\"7ypf/xlj9XXwfDPEoM4URrv/xwf94BcCAzFZH4GiTo0v\", opaque=\"FQhe/qaU925kfnzjCev0ciny7QMkPqMAFRtzCUYo5tdS\"";

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge", challenge);
        diagnostics.Arrange("user", "Mufasa");

        string? value = Answer("Mufasa", "Circle of Life", "/dir/index.html", "f2/wE4q74E6zIJEtWaHKaf5wv/H5QzzpXusqGemxURZJ", challenge);

        string expected = "Digest username=\"Mufasa\", realm=\"http-auth@example.org\", nonce=\"7ypf/xlj9XXwfDPEoM4URrv/xwf94BcCAzFZH4GiTo0v\", "
            + "uri=\"/dir/index.html\", cnonce=\"f2/wE4q74E6zIJEtWaHKaf5wv/H5QzzpXusqGemxURZJ\", nc=00000001, qop=auth, "
            + "response=\"" + response + "\", opaque=\"FQhe/qaU925kfnzjCev0ciny7QMkPqMAFRtzCUYo5tdS\", algorithm=" + algorithm;
        diagnostics.Act("Authorization", value);
        diagnostics.Diff("Authorization", expected, value!);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }

    // The RFC prints a wrong username hash and response for this example; these are the
    // values its errata and an independent computation (Python's hashlib) give, and the
    // username hash is what curl 8.18.0 (Ubuntu, OpenSSL) sent for this user in UTF-8.
    [TestMethod]
    public void CreateAuthorization_Rfc7616Section392_GivesTheCorrectedExampleValues()
    {
        string challenge = "Digest realm=\"api@example.org\", qop=\"auth\", algorithm=SHA-512-256, "
            + "nonce=\"5TsQWLVdgBdmrQ0XsxbDODV+57QdFR34I9HAbC/RVvkK\", opaque=\"HRPCssKJSGjCrkzDg8OhwpzCiGPChXYjwrI2QmXDnsOS\", "
            + "charset=UTF-8, userhash=true";

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge", challenge);
        diagnostics.Arrange("user", "Jäsøn Doe");

        string? value = Answer("Jäsøn Doe", "Secret, or not?", "/doe.json", "NTg6RKcb9boFIAS3KrFK9BGeh+iDa/sm6jUMp2wds69v", challenge);

        const string expected = "Digest username=\"793263caabb707a56211940d90411ea4a575adeccb7e360aeb624ed06ece9b0b\", realm=\"api@example.org\", "
            + "nonce=\"5TsQWLVdgBdmrQ0XsxbDODV+57QdFR34I9HAbC/RVvkK\", uri=\"/doe.json\", "
            + "cnonce=\"NTg6RKcb9boFIAS3KrFK9BGeh+iDa/sm6jUMp2wds69v\", nc=00000001, qop=auth, "
            + "response=\"3798d4131c277846293534c3edc11bd8a5e4cdcbff78b05db9d95eeb1cec68a5\", "
            + "opaque=\"HRPCssKJSGjCrkzDg8OhwpzCiGPChXYjwrI2QmXDnsOS\", algorithm=SHA-512-256, userhash=true";
        diagnostics.Act("Authorization", value);
        diagnostics.Diff("Authorization", expected, value!);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    [DataRow(
        "Mufasa", "Circle Of Life", "AqQxeIb4+11xINQp",
        "Digest realm=\"testrealm@host.com\", qop=\"auth,auth-int\", nonce=\"dcd98b7102dd2f0e8b11d0f600bfb0c093\", opaque=\"5ccc069c403ebaf9f0171e9517f40e41\"",
        "Digest username=\"Mufasa\", realm=\"testrealm@host.com\", nonce=\"dcd98b7102dd2f0e8b11d0f600bfb0c093\", uri=\"/dir/index.html?x=1\", cnonce=\"AqQxeIb4+11xINQp\", nc=00000001, qop=auth, response=\"1c3c228af43ad1467439331b0d52e7a2\", opaque=\"5ccc069c403ebaf9f0171e9517f40e41\"",
        DisplayName = "MD5, qop auth")]
    [DataRow(
        "Mufasa", "Circle of Life", "KJ/mlVGS7TUHDJjH",
        "Digest realm=\"http-auth@example.org\", qop=\"auth, auth-int\", algorithm=SHA-256, nonce=\"7ypf/xlj9XXwfDPEoM4URrv/xwf94BcCAzFZH4GiTo0v\", opaque=\"FQhe/qaU925kfnzjCev0ciny7QMkPqMAFRtzCUYo5tdS\"",
        "Digest username=\"Mufasa\", realm=\"http-auth@example.org\", nonce=\"7ypf/xlj9XXwfDPEoM4URrv/xwf94BcCAzFZH4GiTo0v\", uri=\"/dir/index.html?x=1\", cnonce=\"KJ/mlVGS7TUHDJjH\", nc=00000001, qop=auth, response=\"d39ad8a3303d6b3c571c3f062683f015709d5171f3f2572f50c6e432d6d68e36\", opaque=\"FQhe/qaU925kfnzjCev0ciny7QMkPqMAFRtzCUYo5tdS\", algorithm=SHA-256",
        DisplayName = "SHA-256")]
    [DataRow(
        "Jason", "pw", "nnmPKDzp1C4nRhKe",
        "Digest realm=\"api@example.org\", qop=\"auth\", algorithm=SHA-512-256-sess, nonce=\"5TsQ\", opaque=\"HRPC\"",
        "Digest username=\"Jason\", realm=\"api@example.org\", nonce=\"5TsQ\", uri=\"/dir/index.html?x=1\", cnonce=\"nnmPKDzp1C4nRhKe\", nc=00000001, qop=auth, response=\"8cf7176057726560e946de2bdca341e7a0ef5646cd03f4ed181ebb615134198c\", opaque=\"HRPC\", algorithm=SHA-512-256-sess",
        DisplayName = "SHA-512-256-sess")]
    [DataRow(
        "u", "p", "S9m65TsQDYXSNo9z",
        "Digest realm=\"r\", nonce=\"abc\", algorithm=md5-sess, qop=\"auth-int\"",
        "Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/dir/index.html?x=1\", cnonce=\"S9m65TsQDYXSNo9z\", nc=00000001, qop=auth-int, response=\"d93b6bff3c1f2b0c012c7594732e680e\", algorithm=md5-sess",
        DisplayName = "MD5-sess in lowercase, qop auth-int")]
    [DataRow(
        "u\"x", "p", "unused",
        "Digest realm=\"a\\\"b\", nonce=\"n1\", opaque=\"o\\p\"",
        "Digest username=\"u\\\"x\", realm=\"a\\\"b\", nonce=\"n1\", uri=\"/dir/index.html?x=1\", response=\"e0ac26db0d236b990bc9bf1f9ff702ba\", opaque=\"op\"",
        DisplayName = "No qop; escapes read and written")]
    [DataRow(
        "u", "p", "unused",
        "Digest realm=r r ,nonce=abc,qop=auth ,stale=TRUE",
        "Digest username=\"u\", realm=\"r r \", nonce=\"abc\", uri=\"/dir/index.html?x=1\", response=\"0860136914065042c1164ec0c92f5711\"",
        DisplayName = "Unquoted values keep trailing blanks; 'auth ' is not auth")]
    [DataRow(
        "u", "p", "unused",
        "Digest realm=\"r\", nonce=\"abc\", algorithm=MD5",
        "Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/dir/index.html?x=1\", response=\"dc5a3b958ac976e76d201db0685bb919\", algorithm=MD5",
        DisplayName = "MD5 named, no qop")]
    [DataRow(
        "u", "p", "kAoiW3PrGlPIJtmR",
        "Basic realm=\"x\", Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"",
        "Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/dir/index.html?x=1\", cnonce=\"kAoiW3PrGlPIJtmR\", nc=00000001, qop=auth, response=\"7e065a1df64f0d9b757f3b805eeb998a\"",
        DisplayName = "Digest after Basic in one header")]
    public void CreateAuthorization_MeasuredChallenge_MatchesCurl(string user, string password, string clientNonce, string challenge, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", user);
        diagnostics.Arrange("password", password);
        diagnostics.Arrange("client nonce", clientNonce);
        diagnostics.Arrange("challenge", challenge);

        string? value = Answer(user, password, MeasuredTarget, clientNonce, challenge);

        diagnostics.Act("Authorization", value);
        diagnostics.Diff("Authorization", expected, value!);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }

    // Measured with the user name passed to curl as the Latin-1 bytes 4A E4 73 F8 6E 20 44 6F 65.
    [TestMethod]
    public void CreateAuthorization_MeasuredSha512Slash256UserHash_MatchesCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        DigestAuthenticator authenticator = new(Encoding.Latin1, () => "SRYUJbi5FSpCCXuX");
        HttpAuthRequest request = Request(new NetworkCredential("Jäsøn Doe", "Secret, or not?"), MeasuredTarget, HttpAuthSchemes.Digest);
        const string challenge = "Digest realm=\"api@example.org\", qop=\"auth\", algorithm=SHA-512-256, nonce=\"5TsQWLVdgBdmrQ0XsxbDODV+57QdFR34I9HAbC/RVvkK\", opaque=\"HRPCssKJSGjCrkzDg8OhwpzCiGPChXYjwrI2QmXDnsOS\", charset=UTF-8, userhash=true";
        diagnostics.Arrange("user", "Jäsøn Doe");
        diagnostics.Arrange("challenge", challenge);

        string? value = authenticator.CreateAuthorization(request, [challenge]);

        const string expected = "Digest username=\"e72804befc95fc9e20d714739a58cba0e7e586547dea2d1806e3a34049ad6fcc\", realm=\"api@example.org\", nonce=\"5TsQWLVdgBdmrQ0XsxbDODV+57QdFR34I9HAbC/RVvkK\", uri=\"/dir/index.html?x=1\", cnonce=\"SRYUJbi5FSpCCXuX\", nc=00000001, qop=auth, response=\"35050cfffb7fdc67ac0d998e6eb6fc62e3cfb1870f47aa3ad49de4312cdecb06\", opaque=\"HRPCssKJSGjCrkzDg8OhwpzCiGPChXYjwrI2QmXDnsOS\", algorithm=SHA-512-256, userhash=true";
        diagnostics.Act("Authorization", value);
        diagnostics.Diff("Authorization", expected, value!);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    public void CreateAuthorization_NonAsciiAndControlBytes_ArePercentEscapedAsCurlDoes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", "Jé");
        diagnostics.Arrange("challenge", "Digest realm=\"ré\tx\", nonce=\"n\u0001\", qop=\"AUTH\"");

        string? value = Answer(
            "Jé", "p", "/a%20b/?q=1", "/u5QvFQ91P+JYapd", "Digest realm=\"ré\tx\", nonce=\"n\u0001\", qop=\"AUTH\"");

        const string expected = "Digest username=\"J%C3%A9\", realm=\"r%E9%09x\", nonce=\"n%01\", uri=\"/a%20b/?q=1\", cnonce=\"/u5QvFQ91P+JYapd\", nc=00000001, qop=auth, response=\"e1c2a9f4e7fb40960b16a0ca47f2cb2e\"";
        diagnostics.Act("Authorization", value);
        diagnostics.Diff("Authorization", expected, value!);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    public void CreateAuthorization_UserHashWithoutRealm_HashesAnEmptyRealm()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", "u");
        diagnostics.Arrange("challenge", "Digest nonce=\"abc\", qop=\"auth-int, auth\", userhash=TRUE, algorithm=sha-256-SESS");

        string? value = Answer(
            "u", "p", "/x", "ka9a7qWpSXQL7Jd0", "Digest nonce=\"abc\", qop=\"auth-int, auth\", userhash=TRUE, algorithm=sha-256-SESS");

        const string expected = "Digest username=\"27e14d2b41b03178ea220560f2cedd22275da3e1b3570f9d5957b7b7d96e850c\", realm=\"\", nonce=\"abc\", uri=\"/x\", cnonce=\"ka9a7qWpSXQL7Jd0\", nc=00000001, qop=auth, response=\"795fb2a1774eb8ae2b22a3f2f5e70cd45618f8cf0bc83ff47f3ebe5038ba2d80\", algorithm=sha-256-SESS, userhash=true";
        diagnostics.Act("Authorization", value);
        diagnostics.Diff("Authorization", expected, value!);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    public void CreateAuthorization_TwoDigestHeaders_AnswersTheFirst()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge 1", "Digest realm=\"first\", nonce=\"n1\"");
        diagnostics.Arrange("challenge 2", "Digest realm=\"second\", nonce=\"n2\"");

        string? value = Answer("u", "p", MeasuredTarget, "unused", "Digest realm=\"first\", nonce=\"n1\"", "Digest realm=\"second\", nonce=\"n2\"");

        const string expected = "Digest username=\"u\", realm=\"first\", nonce=\"n1\", uri=\"/dir/index.html?x=1\", response=\"fed44a67fd705927925e252c6e8d370c\"";
        diagnostics.Act("Authorization", value);
        diagnostics.Diff("Authorization", expected, value!);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    [DataRow("Digest realm=\"r\", nonce=\"abc\", algorithm=SHA-1", DisplayName = "Unknown algorithm (measured)")]
    [DataRow("Digest realm=\"r\", nonce=\"abc\", algorithm=MD5-sess", DisplayName = "-sess without qop (measured)")]
    [DataRow("Digest realm=\"r\"", DisplayName = "No nonce (measured)")]
    [DataRow("Digest", DisplayName = "Scheme name alone")]
    [DataRow("Digest,nonce=\"abc\"", DisplayName = "No blank after the scheme name")]
    [DataRow("Basic realm=\"r\"", DisplayName = "No Digest challenge")]
    [DataRow("Digestive nonce=\"abc\"", DisplayName = "A longer scheme name")]
    public void CreateAuthorization_ChallengeCurlRejects_SendsNothing(string challenge)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge", challenge);

        string? value = Answer("u", "p", "/", "unused", challenge);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", null, value);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void CreateAuthorization_RejectedFirstDigest_IgnoresALaterOne()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge 1", "Digest realm=\"r\"");
        diagnostics.Arrange("challenge 2", "Digest nonce=\"abc\"");

        string? value = Answer("u", "p", "/", "unused", "Digest realm=\"r\"", "Digest nonce=\"abc\"");

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", null, value);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void CreateAuthorization_ALongerSchemeNameFirst_AnswersTheDigestInTheNextHeader()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge 1", "Digestive nonce=\"abc\"");
        diagnostics.Arrange("challenge 2", "  Digest\tnonce=\"b\"");

        string? value = Answer("u", "p", "/", "unused", "Digestive nonce=\"abc\"", "  Digest\tnonce=\"b\"");

        const string prefix = "Digest username=\"u\", realm=\"\", nonce=\"b\", uri=\"/\", response=\"";
        diagnostics.Act("Authorization", value);
        diagnostics.Assert("starts with", prefix, value is null ? null : value[..Math.Min(prefix.Length, value.Length)]);
        StringAssert.StartsWith(value, prefix);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Basic, DisplayName = "--basic")]
    [DataRow(HttpAuthSchemes.Bearer, DisplayName = "--oauth2-bearer")]
    [DataRow(HttpAuthSchemes.None, DisplayName = "No scheme")]
    public void CreateAuthorization_DigestNotAllowed_SendsNothing(HttpAuthSchemes allowed)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => "c");
        HttpAuthRequest request = Request(new NetworkCredential("u", "p"), "/", allowed);
        diagnostics.Arrange("allowed schemes", allowed);
        diagnostics.Arrange("challenge", "Digest nonce=\"abc\"");

        string? value = authenticator.CreateAuthorization(request, ["Digest nonce=\"abc\""]);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", null, value);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void CreateAuthorization_NoCredential_SendsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => "c");
        diagnostics.Arrange("credential", null);
        diagnostics.Arrange("challenge", "Digest nonce=\"abc\"");

        string? value = authenticator.CreateAuthorization(Request(null, "/", HttpAuthSchemes.Digest), ["Digest nonce=\"abc\""]);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", null, value);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void CreateAuthorization_BeforeAnyChallenge_SendsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenges", "none");

        string? value = Answer("u", "p", "/", "unused");

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", null, value);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void CreateAuthorization_WindowsCodePage_HashesTheCredentialInIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        DigestAuthenticator authenticator = new(CodePagesEncodingProvider.Instance.GetEncoding(1252)!, () => "c");
        diagnostics.Arrange("user", "é");
        diagnostics.Arrange("code page", 1252);

        string? value = authenticator.CreateAuthorization(
            Request(new NetworkCredential("é", "p"), "/", HttpAuthSchemes.Any), ["Digest nonce=\"abc\""]);

        const string prefix = "Digest username=\"%E9\"";
        diagnostics.Act("Authorization", value);
        diagnostics.Assert("starts with", prefix, value is null ? null : value[..Math.Min(prefix.Length, value.Length)]);
        StringAssert.StartsWith(value, prefix);
    }

    [TestMethod]
    public void CreateAuthorization_NullArguments_Throw()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => "c");
        diagnostics.Arrange("arguments", "null request; null challenges");

        ArgumentNullException nullRequest = Assert.ThrowsExactly<ArgumentNullException>(() => authenticator.CreateAuthorization(null!, []));
        ArgumentNullException nullChallenges = Assert.ThrowsExactly<ArgumentNullException>(
            () => authenticator.CreateAuthorization(Request(null, "/", HttpAuthSchemes.Digest), null!));

        diagnostics.Act("null request exception", nullRequest.GetType().Name);
        diagnostics.Act("null challenges exception", nullChallenges.GetType().Name);
        diagnostics.Assert("null request exception type", nameof(ArgumentNullException), nullRequest.GetType().Name);
        diagnostics.Assert("null challenges exception type", nameof(ArgumentNullException), nullChallenges.GetType().Name);
    }

    /// <summary>
    /// Measured (BL-603 Notes, BL-869): curl 8.21.0 sends a kept proxy Digest answer again with
    /// the same cnonce, <c>nc=00000002</c> and the hash for that count.
    /// </summary>
    [TestMethod]
    public void RepeatAuthorization_MeasuredKeptAnswer_CountsTheNonceOn()
    {
        const string sent = "Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", "
            + "cnonce=\"063231b54c58aa830f9917b0665bdaf8\", nc=00000001, qop=auth, response=\"8146a82aefc2f845325c0b151b67e80d\"";
        var diagnostics = TestDiagnostics.For(TestContext);
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => throw new AssertFailedException("A kept answer keeps its cnonce."));
        diagnostics.Arrange("sent", sent);

        string value = authenticator.RepeatAuthorization(Request(new NetworkCredential("u", "p"), "/", HttpAuthSchemes.Digest), sent);

        const string expected = "Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", "
            + "cnonce=\"063231b54c58aa830f9917b0665bdaf8\", nc=00000002, qop=auth, response=\"924da41f0f75d705a8c76efb5ad7d596\"";
        diagnostics.Act("Authorization", value);
        diagnostics.Diff("Authorization", expected, value);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    public void RepeatAuthorization_SessionAnswerSentTwice_CountsOnFromWhatWasSentWithItsOpaqueAndAlgorithm()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => "c");
        HttpAuthRequest request = Request(new NetworkCredential("u", "p"), "/", HttpAuthSchemes.Digest);
        diagnostics.Arrange("challenge", "Digest realm=\"r\", nonce=\"abc\", qop=\"auth\", opaque=\"o\", algorithm=MD5-sess");
        string first = authenticator.CreateAuthorization(request, ["Digest realm=\"r\", nonce=\"abc\", qop=\"auth\", opaque=\"o\", algorithm=MD5-sess"])!;

        string second = authenticator.RepeatAuthorization(request, first);
        string third = authenticator.RepeatAuthorization(request, second);

        const string expectedFirst = "Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", cnonce=\"c\", nc=00000001, qop=auth, "
            + "response=\"d951e3f1e0c46b81f949b8775a826ec1\", opaque=\"o\", algorithm=MD5-sess";
        const string expectedSecond = "Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", cnonce=\"c\", nc=00000002, qop=auth, "
            + "response=\"fab95e13b86afb93dd4f856f06e18758\", opaque=\"o\", algorithm=MD5-sess";
        diagnostics.Act("first", first);
        diagnostics.Act("second", second);
        diagnostics.Act("third", third);
        diagnostics.Diff("first", expectedFirst, first);
        diagnostics.Diff("second", expectedSecond, second);
        diagnostics.Assert("first", expectedFirst, first);
        diagnostics.Assert("second", expectedSecond, second);
        diagnostics.Assert("third contains nc", true, third.Contains("nc=00000003", StringComparison.Ordinal));
        Assert.AreEqual(expectedFirst, first);
        Assert.AreEqual(expectedSecond, second);
        StringAssert.Contains(third, "nc=00000003", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("Basic dTpw", DisplayName = "Not Digest")]
    [DataRow("", DisplayName = "Sent without a header")]
    [DataRow("Digest username=\"u\", realm=\"r\", uri=\"/\", cnonce=\"c\", nc=00000001, qop=auth, response=\"x\"", DisplayName = "No nonce")]
    [DataRow("Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", response=\"x\"", DisplayName = "No qop")]
    [DataRow("Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", nc=00000001, qop=auth, response=\"x\"", DisplayName = "No cnonce")]
    [DataRow("Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", cnonce=\"c\", qop=auth, response=\"x\"", DisplayName = "No nc")]
    [DataRow("Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", cnonce=\"c\", nc=zz, qop=auth, response=\"x\"", DisplayName = "An nc that is not hexadecimal")]
    public void RepeatAuthorization_NothingToCount_SendsTheValueAsSent(string sent)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => "c");
        diagnostics.Arrange("sent", sent);

        string value = authenticator.RepeatAuthorization(Request(new NetworkCredential("u", "p"), "/", HttpAuthSchemes.Digest), sent);

        diagnostics.Act("Authorization", value);
        diagnostics.Diff("Authorization", sent, value);
        diagnostics.Assert("Authorization", sent, value);
        Assert.AreEqual(sent, value);
    }

    [TestMethod]
    public void RepeatAuthorization_NoCredential_SendsTheValueAsSent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string sent = "Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", cnonce=\"c\", nc=00000001, qop=auth, response=\"x\"";
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => "c");
        diagnostics.Arrange("sent", sent);
        diagnostics.Arrange("credential", null);

        string value = authenticator.RepeatAuthorization(Request(null, "/", HttpAuthSchemes.Digest), sent);

        diagnostics.Act("Authorization", value);
        diagnostics.Diff("Authorization", sent, value);
        diagnostics.Assert("Authorization", sent, value);
        Assert.AreEqual(sent, value);
    }

    [TestMethod]
    public void RepeatAuthorization_NullArguments_Throw()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => "c");
        diagnostics.Arrange("arguments", "null request; null sent value");

        ArgumentNullException nullRequest = Assert.ThrowsExactly<ArgumentNullException>(() => authenticator.RepeatAuthorization(null!, "Basic dTpw"));
        ArgumentNullException nullSent = Assert.ThrowsExactly<ArgumentNullException>(() => authenticator.RepeatAuthorization(Request(null, "/", HttpAuthSchemes.Digest), null!));

        diagnostics.Act("null request exception", nullRequest.GetType().Name);
        diagnostics.Act("null sent exception", nullSent.GetType().Name);
        diagnostics.Assert("null request exception type", nameof(ArgumentNullException), nullRequest.GetType().Name);
        diagnostics.Assert("null sent exception type", nameof(ArgumentNullException), nullSent.GetType().Name);
    }

    /// <summary>
    /// Measured (BL-2022 Notes) with <c>Record-CurlExchange.ps1</c> and
    /// <c>curl --digest -u u:p http://127.0.0.1:{port}/64</c> against curl 8.21.0's Schannel
    /// build: SSPI writes no blank after a comma, algorithm before the response, qop quoted
    /// after it and opaque last.
    /// </summary>
    [TestMethod]
    [DataRow(
        "unused",
        "Digest realm=\"r\", nonce=\"abc\"",
        "Digest username=\"u\",realm=\"r\",nonce=\"abc\",uri=\"/64\",response=\"fc3de222db74c3ec88aabb5510c76f80\"",
        DisplayName = "No qop")]
    [DataRow(
        "unused",
        "Digest realm=\"r\", nonce=\"abc\", opaque=\"op\", algorithm=MD5",
        "Digest username=\"u\",realm=\"r\",nonce=\"abc\",uri=\"/64\",algorithm=MD5,response=\"fc3de222db74c3ec88aabb5510c76f80\",opaque=\"op\"",
        DisplayName = "No qop, opaque and algorithm")]
    [DataRow(
        "1789520a8086d8c6b386c5286eace9d3",
        "Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"",
        "Digest username=\"u\",realm=\"r\",nonce=\"abc\",uri=\"/64\",cnonce=\"1789520a8086d8c6b386c5286eace9d3\",nc=00000001,response=\"7929e56cf7fe409f959989397c308dd3\",qop=\"auth\"",
        DisplayName = "qop=auth")]
    [DataRow(
        "896707c735f53ea0b5a4c92546a776b6",
        "Digest realm=\"r\", nonce=\"abc\", qop=\"auth\", opaque=\"op\", algorithm=MD5",
        "Digest username=\"u\",realm=\"r\",nonce=\"abc\",uri=\"/64\",cnonce=\"896707c735f53ea0b5a4c92546a776b6\",nc=00000001,algorithm=MD5,response=\"6619c920c1ac2d2ec458c3ba51e39426\",qop=\"auth\",opaque=\"op\"",
        DisplayName = "qop=auth, opaque and algorithm")]
    public void CreateAuthorization_SspiBuild_MatchesCurlsSchannelBuild(string clientNonce, string challenge, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => clientNonce, matchesSspiBuild: true);
        diagnostics.Arrange("client nonce", clientNonce);
        diagnostics.Arrange("challenge", challenge);

        string? value = authenticator.CreateAuthorization(Request(new NetworkCredential("u", "p"), "/64", HttpAuthSchemes.Digest), [challenge]);

        diagnostics.Act("Authorization", value);
        diagnostics.Diff("Authorization", expected, value!);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }

    /// <summary>
    /// The OpenSSL build's form for the same exchange as
    /// <see cref="CreateAuthorization_SspiBuild_MatchesCurlsSchannelBuild" />: curl's own
    /// <c>vauth/digest.c</c> puts <c>", "</c> between parameters, qop unquoted before the response.
    /// </summary>
    [TestMethod]
    public void CreateAuthorization_OwnDigestCode_MatchesCurlsOpenSslBuild()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string challenge = "Digest realm=\"r\", nonce=\"abc\", qop=\"auth\", opaque=\"op\", algorithm=MD5";
        diagnostics.Arrange("challenge", challenge);

        string? value = Answer("u", "p", "/64", "896707c735f53ea0b5a4c92546a776b6", challenge);

        const string expected = "Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/64\", cnonce=\"896707c735f53ea0b5a4c92546a776b6\", nc=00000001, qop=auth, "
            + "response=\"6619c920c1ac2d2ec458c3ba51e39426\", opaque=\"op\", algorithm=MD5";
        diagnostics.Act("Authorization", value);
        diagnostics.Diff("Authorization", expected, value!);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    public void RepeatAuthorization_SspiBuildUserHash_CountsOnInTheSspiForm()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => "c", matchesSspiBuild: true);
        HttpAuthRequest request = Request(new NetworkCredential("u", "p"), "/", HttpAuthSchemes.Digest);
        const string challenge = "Digest realm=\"r\", nonce=\"abc\", qop=\"auth\", userhash=true";
        diagnostics.Arrange("challenge", challenge);
        string first = authenticator.CreateAuthorization(request, [challenge])!;

        string second = authenticator.RepeatAuthorization(request, first);

        diagnostics.Act("first", first);
        diagnostics.Act("second", second);
        diagnostics.Assert("second counted on", true, second.Contains(",nc=00000002,", StringComparison.Ordinal));
        diagnostics.Assert("second ends", true, second.EndsWith(",qop=\"auth\",userhash=true", StringComparison.Ordinal));
        StringAssert.Contains(second, ",nc=00000002,", StringComparison.Ordinal);
        StringAssert.EndsWith(second, ",qop=\"auth\",userhash=true", StringComparison.Ordinal);
    }

    private static string? Answer(string user, string password, string target, string clientNonce, params string[] challenges)
    {
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => clientNonce);
        return authenticator.CreateAuthorization(Request(new NetworkCredential(user, password), target, HttpAuthSchemes.Digest), challenges);
    }

    private static HttpAuthRequest Request(NetworkCredential? credential, string target, HttpAuthSchemes allowed) =>
        new("GET", CurlUrl.Parse("http://127.0.0.1" + target), target, credential, null, allowed, IsProxy: false);
}
