namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="DigestChallenge.ReadFirst" /> to the way curl 8.21.0's
/// <c>Curl_auth_decode_digest_http_message</c> and <c>Curl_auth_digest_get_pair</c> read a
/// challenge, including the edges its source defines.
/// </summary>
[TestClass]
public sealed class DigestChallengeTests
{
    [TestMethod]
    public void ReadFirst_EveryParameter_IsRead()
    {
        DigestChallenge? challenge = DigestChallenge.ReadFirst(
            ["Digest REALM=\"r\", Nonce=\"n\", opaque=o, qop=\"auth-int\", algorithm=SHA-256, userhash=true, stale=true, other=\"x\""]);

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
        DigestChallenge? challenge = DigestChallenge.ReadFirst(["Digest nonce=\"n\""]);

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
        Assert.AreEqual("auth", DigestChallenge.ReadFirst(["Digest nonce=n, qop=auth, qop=\"auth-conf\""])?.Qop);
    }

    [TestMethod]
    public void ReadFirst_UserHashTrueThenFalse_StaysTrue()
    {
        Assert.IsTrue(DigestChallenge.ReadFirst(["Digest nonce=n, userhash=TRUE, userhash=false"])?.UserHash);
    }

    [TestMethod]
    public void ReadFirst_UserHashNotTrue_IsFalse()
    {
        Assert.IsFalse(DigestChallenge.ReadFirst(["Digest nonce=n, userhash=yes"])?.UserHash);
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
        Assert.AreEqual(nonce, DigestChallenge.ReadFirst([header])?.Nonce);
    }

    [TestMethod]
    [DataRow("Digest nonce=n, realm=\"r\rx\"", DisplayName = "A line break inside quotes")]
    [DataRow("Digest nonce=n, realm=r\"x", DisplayName = "A quote inside an unquoted value")]
    [DataRow("Digest nonce=n, realm=\"r\\", DisplayName = "A trailing backslash")]
    [DataRow("Digest nonce=n, realm", DisplayName = "No equals sign")]
    [DataRow("Digest nonce=n realm=r", DisplayName = "No comma: the unquoted nonce runs on")]
    public void ReadFirst_MalformedPair_EndsTheListWithoutError(string header)
    {
        DigestChallenge? challenge = DigestChallenge.ReadFirst([header]);

        Assert.IsNotNull(challenge);
        Assert.IsNull(challenge.Realm);
    }

    [TestMethod]
    public void ReadFirst_KeyOf255Characters_IsRead()
    {
        Assert.IsNotNull(DigestChallenge.ReadFirst(["Digest " + new string('k', 255) + "=v, nonce=n"]));
    }

    [TestMethod]
    public void ReadFirst_KeyOf256Characters_EndsTheList()
    {
        Assert.IsNull(DigestChallenge.ReadFirst(["Digest " + new string('k', 256) + "=v, nonce=n"]));
    }

    [TestMethod]
    public void ReadFirst_ValueOver1023Characters_IsCutAt1023AndTheRestReadAsTheNextPair()
    {
        string nonce = new('n', 1023);

        DigestChallenge? challenge = DigestChallenge.ReadFirst(["Digest nonce=" + nonce + "xx=y, realm=r"]);

        Assert.AreEqual(nonce, challenge?.Nonce);
        Assert.AreEqual("r", challenge?.Realm);
    }

    [TestMethod]
    public void ReadFirst_EscapeAsThe1023rdCharacter_IsMalformed()
    {
        string header = "Digest nonce=n, realm=\"" + new string('r', 1022) + "\\x\"";

        Assert.IsNull(DigestChallenge.ReadFirst([header])?.Realm);
    }

    [TestMethod]
    [DataRow("dIgEsT nonce=n", DisplayName = "Any case")]
    [DataRow("Basic realm=\"a\",   Digest nonce=n", DisplayName = "After another scheme")]
    public void ReadFirst_DigestElement_IsFound(string header)
    {
        DigestChallenge? challenge = DigestChallenge.ReadFirst([header]);

        Assert.AreEqual("n", challenge?.Nonce);
    }

    // curl takes "Digest" followed by anything but a letter or digit as the Digest
    // challenge, then rejects it for lacking a blank; the later one is a duplicate.
    [TestMethod]
    public void ReadFirst_DigestFollowedByPunctuation_IsTheFirstAndIsRejected()
    {
        Assert.IsNull(DigestChallenge.ReadFirst(["Digest-x, Digest nonce=n"]));
    }

    [TestMethod]
    public void ReadFirst_EmptyHeaderValues_FindNothing()
    {
        Assert.IsNull(DigestChallenge.ReadFirst(["", " ", "Basic,"]));
    }
}
