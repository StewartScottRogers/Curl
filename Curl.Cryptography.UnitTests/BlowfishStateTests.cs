namespace Curl.Cryptography;

/// <summary>
/// Checks <see cref="BlowfishState" />'s pieces that the published vectors only reach
/// indirectly: the digits of pi it starts from and OpenBSD's <c>Blowfish_stream2word</c>.
/// </summary>
[TestClass]
public sealed class BlowfishStateTests
{
    // Schneier 1993: P1 and the first word of S1 are the first hexadecimal digits of pi's fraction; S4[255] ends the table.
    [TestMethod]
    public void PiDigits_FirstAndLastWords_AreThePublishedOnes()
    {
        Assert.HasCount(18, BlowfishPiDigits.Subkeys.ToArray());
        Assert.HasCount(1024, BlowfishPiDigits.SubstitutionBoxes.ToArray());
        Assert.AreEqual(0x243F6A88u, BlowfishPiDigits.Subkeys[0]);
        Assert.AreEqual(0x8979FB1Bu, BlowfishPiDigits.Subkeys[17]);
        Assert.AreEqual(0xD1310BA6u, BlowfishPiDigits.SubstitutionBoxes[0]);
        Assert.AreEqual(0x3AC372E6u, BlowfishPiDigits.SubstitutionBoxes[1023]);
    }

    [TestMethod]
    public void ReadWord_DataShorterThanAWord_WrapsToItsStartBigEndian()
    {
        int position = 0;

        uint first = BlowfishState.ReadWord([0x01, 0x02, 0x03], ref position);
        uint second = BlowfishState.ReadWord([0x01, 0x02, 0x03], ref position);

        Assert.AreEqual(0x01020301u, first);
        Assert.AreEqual(0x02030102u, second);
        Assert.AreEqual(2, position);
    }

    [TestMethod]
    public void ReadWord_EmptyData_IsZero()
    {
        int position = 0;

        uint word = BlowfishState.ReadWord([], ref position);

        Assert.AreEqual(0u, word);
    }

    [TestMethod]
    public void EncryptThenDecrypt_AfterKeySchedule_RoundTrips()
    {
        BlowfishState state = new();
        state.Initialize();
        state.ExpandKey("key"u8);
        uint left = 0x01234567;
        uint right = 0x89ABCDEF;

        state.Encrypt(ref left, ref right);
        state.Decrypt(ref left, ref right);

        Assert.AreEqual(0x01234567u, left);
        Assert.AreEqual(0x89ABCDEFu, right);
    }
}
