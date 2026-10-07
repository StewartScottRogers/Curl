using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Checks <see cref="BlowfishState" />'s pieces that the published vectors only reach
/// indirectly: the digits of pi it starts from and OpenBSD's <c>Blowfish_stream2word</c>.
/// </summary>
[TestClass]
public sealed class BlowfishStateTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // Schneier 1993: P1 and the first word of S1 are the first hexadecimal digits of pi's fraction; S4[255] ends the table.
    [TestMethod]
    public void PiDigits_FirstAndLastWords_AreThePublishedOnes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", "Schneier 1993 hexadecimal digits of pi");

        int subkeyCount = BlowfishPiDigits.Subkeys.ToArray().Length;
        int boxWordCount = BlowfishPiDigits.SubstitutionBoxes.ToArray().Length;
        diagnostics.Act("subkey count", subkeyCount);
        diagnostics.Act("substitution box word count", boxWordCount);
        diagnostics.Act("P[0]", $"0x{BlowfishPiDigits.Subkeys[0]:X8}");
        diagnostics.Act("P[17]", $"0x{BlowfishPiDigits.Subkeys[17]:X8}");
        diagnostics.Act("S[0]", $"0x{BlowfishPiDigits.SubstitutionBoxes[0]:X8}");
        diagnostics.Act("S[1023]", $"0x{BlowfishPiDigits.SubstitutionBoxes[1023]:X8}");

        diagnostics.Assert("subkey count", 18, subkeyCount);
        diagnostics.Assert("substitution box word count", 1024, boxWordCount);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        int position = 0;
        diagnostics.Arrange("data", "01 02 03");
        diagnostics.Arrange("start position", position);

        uint first = BlowfishState.ReadWord([0x01, 0x02, 0x03], ref position);
        uint second = BlowfishState.ReadWord([0x01, 0x02, 0x03], ref position);
        diagnostics.Act("first word", $"0x{first:X8}");
        diagnostics.Act("second word", $"0x{second:X8}");
        diagnostics.Act("position", position);

        diagnostics.Assert("first word", 0x01020301u, first);
        diagnostics.Assert("second word", 0x02030102u, second);
        diagnostics.Assert("position", 2, position);
        Assert.AreEqual(0x01020301u, first);
        Assert.AreEqual(0x02030102u, second);
        Assert.AreEqual(2, position);
    }

    [TestMethod]
    public void ReadWord_EmptyData_IsZero()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        int position = 0;
        diagnostics.Arrange("data", "empty");

        uint word = BlowfishState.ReadWord([], ref position);
        diagnostics.Act("word", word);

        diagnostics.Assert("word", 0u, word);
        Assert.AreEqual(0u, word);
    }

    [TestMethod]
    public void EncryptThenDecrypt_AfterKeySchedule_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        BlowfishState state = new();
        state.Initialize();
        state.ExpandKey("key"u8);
        uint left = 0x01234567;
        uint right = 0x89ABCDEF;
        diagnostics.Arrange("key", "key");
        diagnostics.Arrange("left", $"0x{left:X8}");
        diagnostics.Arrange("right", $"0x{right:X8}");

        state.Encrypt(ref left, ref right);
        diagnostics.Act("encrypted left", $"0x{left:X8}");
        diagnostics.Act("encrypted right", $"0x{right:X8}");
        state.Decrypt(ref left, ref right);
        diagnostics.Act("decrypted left", $"0x{left:X8}");
        diagnostics.Act("decrypted right", $"0x{right:X8}");

        diagnostics.Assert("left", 0x01234567u, left);
        diagnostics.Assert("right", 0x89ABCDEFu, right);
        Assert.AreEqual(0x01234567u, left);
        Assert.AreEqual(0x89ABCDEFu, right);
    }

    // The masked scan must pick the same entries a direct look-up does, at every index of every S-box.
    [TestMethod]
    public void Mix_EveryIndexOfEveryBox_EqualsTheDirectLookUpFormula()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        BlowfishState state = new();
        state.Initialize();
        ReadOnlySpan<uint> boxes = BlowfishPiDigits.SubstitutionBoxes;
        diagnostics.Arrange("indexes checked", 256);
        int mismatches = 0;

        for (uint value = 0; value < 256; value++)
        {
            uint a = value;
            uint b = (value + 85) & 0xFF;
            uint c = (value + 170) & 0xFF;
            uint d = 255 - value;
            uint expected = ((boxes[(int)a] + boxes[256 + (int)b]) ^ boxes[512 + (int)c]) + boxes[768 + (int)d];
            uint actual = state.Mix((a << 24) | (b << 16) | (c << 8) | d);
            if (actual != expected)
            {
                mismatches++;
                diagnostics.Act($"mismatch at index {value}", $"expected 0x{expected:X8}, actual 0x{actual:X8}");
            }

            Assert.AreEqual(expected, actual, $"index {value}");
        }

        diagnostics.Act("mismatching indexes", mismatches);
        diagnostics.Assert("mismatching indexes", 0, mismatches);
    }
}
