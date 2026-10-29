namespace Curl.Extension.OAuth2.Wire;

/// <summary>Pins the PKCE verifier and <c>S256</c> challenge against RFC 7636 §4.1, §4.2 and Appendix B.</summary>
[TestClass]
public sealed class OAuth2PkceTests
{
    // RFC 7636 Appendix B: the 32 random octets.
    private static readonly byte[] AppendixBOctets =
    [
        116, 24, 223, 180, 151, 153, 224, 37, 79, 250, 96, 125, 216, 173, 187, 186,
        22, 212, 37, 77, 105, 214, 191, 240, 91, 88, 5, 88, 83, 132, 141, 121,
    ];

    [TestMethod]
    public void CreateCodeVerifier_AppendixBOctets_MakesAppendixBVerifier()
    {
        Assert.AreEqual("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk", OAuth2Pkce.CreateCodeVerifier(AppendixBOctets));
    }

    [TestMethod]
    public void CreateS256CodeChallenge_AppendixBVerifier_MakesAppendixBChallenge()
    {
        Assert.AreEqual(
            "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            OAuth2Pkce.CreateS256CodeChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    [TestMethod]
    public void CreateCodeVerifier_NinetySixOctets_MakesTheLongestVerifierSection41Allows()
    {
        Assert.AreEqual(128, OAuth2Pkce.CreateCodeVerifier(new byte[96]).Length);
    }

    [TestMethod]
    [DataRow(31)]
    [DataRow(97)]
    public void CreateCodeVerifier_OctetCountOutsideSection41_Throws(int count)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => OAuth2Pkce.CreateCodeVerifier(new byte[count]));
    }
}
