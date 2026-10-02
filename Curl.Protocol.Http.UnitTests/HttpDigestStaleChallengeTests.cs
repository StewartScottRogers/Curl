namespace Curl.Protocol.Http;

/// <summary>
/// Pins how <see cref="HttpDigestStaleChallenge" /> reads <c>stale=true</c> from a
/// <c>401</c>'s or <c>407</c>'s challenge values (BL-1148).
/// </summary>
[TestClass]
public sealed class HttpDigestStaleChallengeTests
{
    [TestMethod]
    [DataRow("Digest realm=\"r\", nonce=\"b\", stale=true", DisplayName = "bare true")]
    [DataRow("  digest nonce=\"b\", STALE = \"TRUE\"  ", DisplayName = "quoted, any case, blanks")]
    [DataRow("Digest\tstale=true", DisplayName = "tab after the scheme")]
    public void IsOfferedIn_FindsAStaleDigestChallenge(string challenge)
    {
        Assert.IsTrue(HttpDigestStaleChallenge.IsOfferedIn(["Basic realm=\"r\"", challenge]));
    }

    [TestMethod]
    [DataRow("Digest realm=\"r\", nonce=\"b\", stale=false", DisplayName = "stale false")]
    [DataRow("Digest realm=\"r\", nonce=\"b\"", DisplayName = "no stale")]
    [DataRow("Digest", DisplayName = "scheme alone")]
    [DataRow("Digestx stale=true", DisplayName = "another scheme starting Digest")]
    [DataRow("Basic stale=true", DisplayName = "not Digest")]
    [DataRow("Digest =true, stale, nonce=\"stale=true\"", DisplayName = "no key, no value")]
    public void IsOfferedIn_FindsNoStaleDigestChallenge(string challenge)
    {
        Assert.IsFalse(HttpDigestStaleChallenge.IsOfferedIn([challenge]));
    }

    [TestMethod]
    public void IsOfferedIn_FindsNothingInNoChallenges()
    {
        Assert.IsFalse(HttpDigestStaleChallenge.IsOfferedIn([]));
    }
}
