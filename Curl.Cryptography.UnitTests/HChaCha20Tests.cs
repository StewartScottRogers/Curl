namespace Curl.Cryptography;

/// <summary>Pins <see cref="HChaCha20" /> to draft-irtf-cfrg-xchacha-03 section 2.2.1.</summary>
[TestClass]
public sealed class HChaCha20Tests
{
    [TestMethod]
    public void DeriveSubkey_DraftSection221_MatchesResultantSubkey()
    {
        byte[] key = Convert.FromHexString("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f");
        byte[] nonce = Convert.FromHexString("000000090000004a0000000031415927");
        byte[] subkey = new byte[HChaCha20.SubkeySize];

        HChaCha20.DeriveSubkey(key, nonce, subkey);

        Assert.AreEqual("82413b4227b27bfed30e42508a877d73a0f9e4d58a74a853c12ec41326d3ecdc", Convert.ToHexStringLower(subkey));
    }

    [TestMethod]
    [DataRow(31, 16, 32)]
    [DataRow(32, 12, 32)]
    [DataRow(32, 16, 16)]
    public void DeriveSubkey_WrongLength_Throws(int keyLength, int nonceLength, int subkeyLength) =>
        Assert.ThrowsExactly<ArgumentException>(
            () => HChaCha20.DeriveSubkey(new byte[keyLength], new byte[nonceLength], new byte[subkeyLength]));
}
