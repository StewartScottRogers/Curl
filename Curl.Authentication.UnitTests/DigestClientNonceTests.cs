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
}
