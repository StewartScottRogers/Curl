namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="Aria" /> to RFC 5794 Appendix A's test vectors for 128-, 192- and
/// 256-bit keys, checks its S-boxes and diffusion layer against the properties RFC 5794
/// states, and checks its argument and disposal rules (ADR-0118).
/// </summary>
[TestClass]
public sealed class AriaTests
{
    private const string AppendixPlaintext = "00112233445566778899AABBCCDDEEFF";

    // RFC 5794 Appendix A.1 to A.3: key, then the ciphertext of 00112233445566778899aabbccddeeff.
    [TestMethod]
    [DataRow("000102030405060708090A0B0C0D0E0F", "D718FBD6AB644C739DA95F3BE6451778")]
    [DataRow("000102030405060708090A0B0C0D0E0F1011121314151617", "26449C1805DBE7AA25A468CE263A9E79")]
    [DataRow("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F", "F92BD7C79FB72E2F2B8F80C1972D24FC")]
    public void EncryptBlockAndDecryptBlock_AppendixAVector_GiveThePublishedCiphertextAndPlaintext(string key, string ciphertext)
    {
        using Aria aria = new(Convert.FromHexString(key));
        byte[] encrypted = new byte[Aria.BlockSize];
        byte[] decrypted = new byte[Aria.BlockSize];

        aria.EncryptBlock(Convert.FromHexString(AppendixPlaintext), encrypted);
        aria.DecryptBlock(Convert.FromHexString(ciphertext), decrypted);

        Assert.AreEqual(ciphertext, Convert.ToHexString(encrypted));
        Assert.AreEqual(AppendixPlaintext, Convert.ToHexString(decrypted));
    }

    [TestMethod]
    [DataRow(16, 13)]
    [DataRow(24, 15)]
    [DataRow(32, 17)]
    public void Constructor_KeyLength_GivesOneRoundKeyMoreThanTheRounds(int keyLength, int roundKeys)
    {
        using Aria aria = new(new byte[keyLength]);

        Assert.AreEqual(roundKeys, aria.EncryptionRoundKeys.Length);
        Assert.AreEqual(roundKeys, aria.DecryptionRoundKeys.Length);
    }

    [TestMethod]
    public void EncryptBlockThenDecryptBlock_InPlace_RoundTrips()
    {
        using Aria aria = new(Convert.FromHexString("000102030405060708090A0B0C0D0E0F"));
        byte[] buffer = Convert.FromHexString(AppendixPlaintext);

        aria.EncryptBlock(buffer, buffer);
        Assert.AreEqual("D718FBD6AB644C739DA95F3BE6451778", Convert.ToHexString(buffer));
        aria.DecryptBlock(buffer, buffer);

        Assert.AreEqual(AppendixPlaintext, Convert.ToHexString(buffer));
    }

    // RFC 5794 section 2.4.2: SB1(0x23) = 0x26 and SB4(0xef) = 0xd3; SL1 puts byte 0
    // through SB1 and byte 3 through SB4.
    [TestMethod]
    public void Substitute_Section242Examples_GiveThePublishedOutputs()
    {
        UInt128 substituted = Aria.Substitute(new UInt128(0x230000EF00000000UL, 0), oddRound: true);

        Assert.AreEqual(0x26UL, (ulong)(substituted >> 120));
        Assert.AreEqual(0xD3UL, (ulong)(substituted >> 96) & 0xFF);
    }

    // RFC 5794 section 2.4.2: SB3 and SB4 are the inverses of SB1 and SB2, so SL2 inverts SL1.
    [TestMethod]
    public void Substitute_EveryByteThroughSl1ThenSl2_GivesTheByteBack()
    {
        for (int value = 0; value < 256; value++)
        {
            UInt128 input = UInt128.MaxValue / 255 * (uint)value;

            Assert.AreEqual(input, Aria.Substitute(Aria.Substitute(input, oddRound: true), oddRound: false));
            Assert.AreEqual(input, Aria.Substitute(Aria.Substitute(input, oddRound: false), oddRound: true));
        }
    }

    // RFC 5794 section 2.4.3: A is an involution, and x0 feeds y3, y4, y6, y8, y9, y13 and y14.
    [TestMethod]
    public void Diffuse_AppliedTwice_GivesTheInputBackAndSetsTheBytesSection243Lists()
    {
        UInt128 input = new(0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL);
        UInt128 byteZero = new(0x0100000000000000UL, 0);

        Assert.AreEqual(input, Aria.Diffuse(Aria.Diffuse(input)));
        Assert.AreEqual(new UInt128(0x0000000101000100UL, 0x0101000000010100UL), Aria.Diffuse(byteZero));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(15)]
    [DataRow(17)]
    [DataRow(33)]
    public void Constructor_KeyNotSixteenTwentyFourOrThirtyTwoBytes_Throws(int keyLength)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new Aria(new byte[keyLength]));

        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    [DataRow(15, 16, "source")]
    [DataRow(16, 17, "destination")]
    public void EncryptBlockAndDecryptBlock_WrongLength_Throws(int sourceLength, int destinationLength, string parameterName)
    {
        using Aria aria = new(new byte[16]);

        ArgumentException encrypt = Assert.ThrowsExactly<ArgumentException>(() => aria.EncryptBlock(new byte[sourceLength], new byte[destinationLength]));
        ArgumentException decrypt = Assert.ThrowsExactly<ArgumentException>(() => aria.DecryptBlock(new byte[sourceLength], new byte[destinationLength]));

        Assert.AreEqual(parameterName, encrypt.ParamName);
        Assert.AreEqual(parameterName, decrypt.ParamName);
    }

    [TestMethod]
    public void Dispose_ZeroesTheRoundKeysAndLaterCallsThrow()
    {
        Aria aria = new(Convert.FromHexString("000102030405060708090A0B0C0D0E0F"));

        aria.Dispose();

        Assert.IsTrue(aria.EncryptionRoundKeys.ToArray().All(key => key == UInt128.Zero));
        Assert.IsTrue(aria.DecryptionRoundKeys.ToArray().All(key => key == UInt128.Zero));
        Assert.ThrowsExactly<ObjectDisposedException>(() => aria.EncryptBlock(new byte[16], new byte[16]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => aria.DecryptBlock(new byte[16], new byte[16]));
    }
}
