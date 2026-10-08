using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="DigestChallenge.ReadFirst" /> to the way curl 8.21.0's
/// <c>Curl_auth_decode_digest_http_message</c> and <c>Curl_auth_digest_get_pair</c> read a
/// challenge, including the edges its source defines.
/// </summary>
[TestClass]
public sealed class DigestChallengeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ReadFirst_EveryParameter_IsRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string header = "Digest REALM=\"r\", Nonce=\"n\", opaque=o, qop=\"auth-int\", algorithm=SHA-256, userhash=true, stale=true, other=\"x\"";
        diagnostics.Arrange("challenge header", header);

        DigestChallenge? challenge = DigestChallenge.ReadFirst([header]);

        diagnostics.Act("realm", challenge?.Realm);
        diagnostics.Act("nonce", challenge?.Nonce);
        diagnostics.Act("opaque", challenge?.Opaque);
        diagnostics.Act("qop", challenge?.Qop);
        diagnostics.Act("algorithm", challenge?.AlgorithmName);
        diagnostics.Act("user hash", challenge?.UserHash);
        diagnostics.Assert("realm", "r", challenge?.Realm);
        diagnostics.Assert("nonce", "n", challenge?.Nonce);
        diagnostics.Assert("opaque", "o", challenge?.Opaque);
        diagnostics.Assert("qop", "auth-int", challenge?.Qop);
        diagnostics.Assert("algorithm", "SHA-256", challenge?.AlgorithmName);
        diagnostics.Assert("user hash", true, challenge?.UserHash);
        Assert.IsNotNull(challenge);
        Assert.AreEqual("r", challenge.Realm);
        Assert.AreEqual("n", challenge.Nonce);
        Assert.AreEqual("o", challenge.Opaque);
        Assert.AreEqual("auth-int", challenge.Qop);
        Assert.AreEqual("SHA-256", challenge.AlgorithmName);
        Assert.IsFalse(challenge.Algorithm.IsSession);
        Assert.IsTrue(challenge.UserHash);
    }

    [TestMethod]
    public void ReadFirst_OnlyANonce_DefaultsToMd5AndNothingElse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge header", "Digest nonce=\"n\"");

        DigestChallenge? challenge = DigestChallenge.ReadFirst(["Digest nonce=\"n\""]);

        diagnostics.Act("nonce", challenge?.Nonce);
        diagnostics.Act("realm", challenge?.Realm);
        diagnostics.Act("algorithm is Md5", ReferenceEquals(challenge?.Algorithm, DigestAlgorithm.Md5));
        diagnostics.Act("user hash", challenge?.UserHash);
        diagnostics.Assert("nonce", "n", challenge?.Nonce);
        diagnostics.Assert("realm", null, challenge?.Realm);
        diagnostics.Assert("user hash", false, challenge?.UserHash);
        Assert.IsNotNull(challenge);
        Assert.AreEqual("n", challenge.Nonce);
        Assert.IsNull(challenge.Realm);
        Assert.IsNull(challenge.Opaque);
        Assert.IsNull(challenge.Qop);
        Assert.IsNull(challenge.AlgorithmName);
        Assert.AreSame(DigestAlgorithm.Md5, challenge.Algorithm);
        Assert.IsFalse(challenge.UserHash);
    }

    [TestMethod]
    public void ReadFirst_QopWithNothingCurlAnswers_KeepsTheEarlierChoice()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge header", "Digest nonce=n, qop=auth, qop=\"auth-conf\"");

        string? qop = DigestChallenge.ReadFirst(["Digest nonce=n, qop=auth, qop=\"auth-conf\""])?.Qop;

        diagnostics.Act("qop", qop);
        diagnostics.Assert("qop", "auth", qop);
        Assert.AreEqual("auth", qop);
    }

    [TestMethod]
    public void ReadFirst_UserHashTrueThenFalse_StaysTrue()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge header", "Digest nonce=n, userhash=TRUE, userhash=false");

        bool? userHash = DigestChallenge.ReadFirst(["Digest nonce=n, userhash=TRUE, userhash=false"])?.UserHash;

        diagnostics.Act("user hash", userHash);
        diagnostics.Assert("user hash", true, userHash);
        Assert.IsTrue(userHash);
    }

    [TestMethod]
    public void ReadFirst_UserHashNotTrue_IsFalse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge header", "Digest nonce=n, userhash=yes");

        bool? userHash = DigestChallenge.ReadFirst(["Digest nonce=n, userhash=yes"])?.UserHash;

        diagnostics.Act("user hash", userHash);
        diagnostics.Assert("user hash", false, userHash);
        Assert.IsFalse(userHash);
    }

    [TestMethod]
    [DataRow("Digest nonce=\"a\\\\b\\,c\"", "a\\b,c", DisplayName = "Backslash escapes inside quotes")]
    [DataRow("Digest nonce=a\\b", "a\\b", DisplayName = "A backslash outside quotes is kept")]
    [DataRow("Digest nonce=\"unterminated", "unterminated", DisplayName = "A missing closing quote ends at the end")]
    [DataRow("Digest nonce=a b\r\nrealm=r", "a b", DisplayName = "An unquoted value ends at a line break")]
    [DataRow("Digest nonce=", "", DisplayName = "An empty value")]
    [DataRow("Digest nonce=\"\"", "", DisplayName = "An empty quoted value")]
    [DataRow("Digest \t nonce=\"n\" \t,\t realm=r", "n", DisplayName = "Blanks around the comma")]
    public void ReadFirst_Value_IsReadAsCurlReadsIt(string header, string nonce)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge header", header);

        string? actual = DigestChallenge.ReadFirst([header])?.Nonce;

        diagnostics.Act("nonce", actual);
        diagnostics.Assert("nonce", nonce, actual);
        Assert.AreEqual(nonce, actual);
    }

    [TestMethod]
    [DataRow("Digest nonce=n, realm=\"r\rx\"", DisplayName = "A line break inside quotes")]
    [DataRow("Digest nonce=n, realm=r\"x", DisplayName = "A quote inside an unquoted value")]
    [DataRow("Digest nonce=n, realm=\"r\\", DisplayName = "A trailing backslash")]
    [DataRow("Digest nonce=n, realm", DisplayName = "No equals sign")]
    [DataRow("Digest nonce=n realm=r", DisplayName = "No comma: the unquoted nonce runs on")]
    public void ReadFirst_MalformedPair_EndsTheListWithoutError(string header)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge header", header);

        DigestChallenge? challenge = DigestChallenge.ReadFirst([header]);

        diagnostics.Act("challenge read", challenge is not null);
        diagnostics.Act("realm", challenge?.Realm);
        diagnostics.Assert("challenge read", true, challenge is not null);
        diagnostics.Assert("realm", null, challenge?.Realm);
        Assert.IsNotNull(challenge);
        Assert.IsNull(challenge.Realm);
    }

    [TestMethod]
    public void ReadFirst_KeyOf255Characters_IsRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key length", 255);

        DigestChallenge? challenge = DigestChallenge.ReadFirst(["Digest " + new string('k', 255) + "=v, nonce=n"]);

        diagnostics.Act("challenge read", challenge is not null);
        diagnostics.Assert("challenge read", true, challenge is not null);
        Assert.IsNotNull(challenge);
    }

    [TestMethod]
    public void ReadFirst_KeyOf256Characters_EndsTheList()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key length", 256);

        DigestChallenge? challenge = DigestChallenge.ReadFirst(["Digest " + new string('k', 256) + "=v, nonce=n"]);

        diagnostics.Act("challenge read", challenge is not null);
        diagnostics.Assert("challenge read", false, challenge is not null);
        Assert.IsNull(challenge);
    }

    [TestMethod]
    public void ReadFirst_ValueOver1023Characters_IsCutAt1023AndTheRestReadAsTheNextPair()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string nonce = new('n', 1023);
        diagnostics.Arrange("nonce length", nonce.Length);

        DigestChallenge? challenge = DigestChallenge.ReadFirst(["Digest nonce=" + nonce + "xx=y, realm=r"]);

        diagnostics.Act("nonce length", challenge?.Nonce?.Length);
        diagnostics.Act("realm", challenge?.Realm);
        diagnostics.Assert("nonce length", nonce.Length, challenge?.Nonce?.Length);
        diagnostics.Assert("realm", "r", challenge?.Realm);
        Assert.AreEqual(nonce, challenge?.Nonce);
        Assert.AreEqual("r", challenge?.Realm);
    }

    [TestMethod]
    public void ReadFirst_EscapeAsThe1023rdCharacter_IsMalformed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string header = "Digest nonce=n, realm=\"" + new string('r', 1022) + "\\x\"";
        diagnostics.Arrange("realm characters before the escape", 1022);

        string? realm = DigestChallenge.ReadFirst([header])?.Realm;

        diagnostics.Act("realm", realm);
        diagnostics.Assert("realm", null, realm);
        Assert.IsNull(realm);
    }

    [TestMethod]
    [DataRow("dIgEsT nonce=n", DisplayName = "Any case")]
    [DataRow("Basic realm=\"a\",   Digest nonce=n", DisplayName = "After another scheme")]
    public void ReadFirst_DigestElement_IsFound(string header)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge header", header);

        DigestChallenge? challenge = DigestChallenge.ReadFirst([header]);

        diagnostics.Act("nonce", challenge?.Nonce);
        diagnostics.Assert("nonce", "n", challenge?.Nonce);
        Assert.AreEqual("n", challenge?.Nonce);
    }

    // curl takes "Digest" followed by anything but a letter or digit as the Digest
    // challenge, then rejects it for lacking a blank; the later one is a duplicate.
    [TestMethod]
    public void ReadFirst_DigestFollowedByPunctuation_IsTheFirstAndIsRejected()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge header", "Digest-x, Digest nonce=n");

        DigestChallenge? challenge = DigestChallenge.ReadFirst(["Digest-x, Digest nonce=n"]);

        diagnostics.Act("challenge read", challenge is not null);
        diagnostics.Assert("challenge read", false, challenge is not null);
        Assert.IsNull(challenge);
    }

    [TestMethod]
    public void ReadFirst_EmptyHeaderValues_FindNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge headers", "\"\", \" \", \"Basic,\"");

        DigestChallenge? challenge = DigestChallenge.ReadFirst(["", " ", "Basic,"]);

        diagnostics.Act("challenge read", challenge is not null);
        diagnostics.Assert("challenge read", false, challenge is not null);
        Assert.IsNull(challenge);
    }
}
