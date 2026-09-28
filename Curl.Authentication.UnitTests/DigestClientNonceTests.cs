namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="DigestClientNonce" /> to curl 8.21.0's cnonce shape: 12 random bytes in
/// base64.
/// </summary>
[TestClass]
public sealed class DigestClientNonceTests
{
    [TestMethod]
    public void CreateRandom_Called_Gives12BytesInBase64()
    {
        string clientNonce = DigestClientNonce.CreateRandom();

        Assert.HasCount(12, Convert.FromBase64String(clientNonce));
        Assert.AreEqual(16, clientNonce.Length);
    }

    [TestMethod]
    public void CreateRandom_CalledTwice_GivesDifferentValues()
    {
        Assert.AreNotEqual(DigestClientNonce.CreateRandom(), DigestClientNonce.CreateRandom());
    }

    [TestMethod]
    public void CreateRandomHex_Called_Gives32LowerCaseHexDigits()
    {
        // Measured: cnonce="81eed5b913007ab96776b8224946a866" (SSPI), "dab6bbea0a329577a0c691f97f35a087" (OpenSSL).
        string clientNonce = DigestClientNonce.CreateRandomHex();

        Assert.AreEqual(32, clientNonce.Length);
        Assert.IsTrue(clientNonce.All(character => char.IsAsciiHexDigitLower(character) || char.IsAsciiDigit(character)), clientNonce);
        Assert.AreNotEqual(clientNonce, DigestClientNonce.CreateRandomHex());
    }
}
