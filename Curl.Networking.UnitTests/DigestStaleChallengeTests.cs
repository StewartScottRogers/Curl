using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="DigestStaleChallenge" /> reads <c>stale=true</c> from a <c>407</c>'s
/// <c>Proxy-Authenticate</c> values (BL-864).
/// </summary>
[TestClass]
public sealed class DigestStaleChallengeTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("Digest realm=\"r\", nonce=\"b\", stale=true", DisplayName = "bare true")]
    [DataRow("  digest nonce=\"b\", STALE = \"TRUE\"  ", DisplayName = "quoted, any case, blanks")]
    [DataRow("Digest\tstale=true", DisplayName = "tab after the scheme")]
    public void IsOfferedIn_FindsAStaleDigestChallenge(string challenge)
    {
        Diagnostics.Arrange("challenge", challenge);
        Diagnostics.Arrange("other challenge", "Basic realm=\"r\"");

        var offered = DigestStaleChallenge.IsOfferedIn(["Basic realm=\"r\"", challenge]);

        Diagnostics.Act("stale offered", offered);
        Diagnostics.Assert("stale offered", true, offered);
        Assert.IsTrue(DigestStaleChallenge.IsOfferedIn(["Basic realm=\"r\"", challenge]));
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
        Diagnostics.Arrange("challenge", challenge);

        var offered = DigestStaleChallenge.IsOfferedIn([challenge]);

        Diagnostics.Act("stale offered", offered);
        Diagnostics.Assert("stale offered", false, offered);
        Assert.IsFalse(DigestStaleChallenge.IsOfferedIn([challenge]));
    }

    [TestMethod]
    public void IsOfferedIn_FindsNothingInNoChallenges()
    {
        Diagnostics.Arrange("challenge count", 0);

        var offered = DigestStaleChallenge.IsOfferedIn([]);

        Diagnostics.Act("stale offered", offered);
        Diagnostics.Assert("stale offered", false, offered);
        Assert.IsFalse(DigestStaleChallenge.IsOfferedIn([]));
    }
}
