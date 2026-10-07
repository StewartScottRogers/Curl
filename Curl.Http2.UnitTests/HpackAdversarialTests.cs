using Curl.Testing;
using static Curl.Http2.Hpack;

namespace Curl.Http2;

/// <summary>
/// Adversarial black-box attacks on the HPACK decoder, encoder and Huffman coder (BL-1499,
/// by the method in Documentation/Wiki/Adversarial-Testing.md): integers at and past 2^31 - 1,
/// indexes just outside both tables, Huffman strings with EOS or bad padding, table size
/// updates out of order and past the limit, truncated and bit-flipped blocks, and long
/// seeded encoder-to-decoder runs with the table size changing under them. The oracle is
/// RFC 7541 and the decoder's documented refusals.
/// </summary>
[TestClass]
public sealed class HpackAdversarialTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // An indexed field whose index is 127 + 0 + 127 * 2^7 + 127 * 2^14 + 127 * 2^21 + 7 * 2^28 = 2^31 - 1.
    [TestMethod]
    public void Decode_IndexedFieldAtLargestInteger_RefusesAsInvalidIndex()
    {
        AssertDecodeRefused("FF 80 FF FF FF 07", HpackDecodingError.InvalidIndex);
    }

    [TestMethod]
    public void Decode_IndexedFieldOnePastLargestInteger_RefusesAsIntegerOverflow()
    {
        AssertDecodeRefused("FF 81 FF FF FF 07", HpackDecodingError.IntegerOverflow);
    }

    [TestMethod]
    public void Decode_IntegerPaddedWithSixZeroContinuationOctets_RefusesAsIntegerOverflow()
    {
        AssertDecodeRefused("FF 80 80 80 80 80 00", HpackDecodingError.IntegerOverflow);
    }

    [TestMethod]
    public void Decode_BlockEndingInsideIntegerContinuation_RefusesAsTruncatedBlock()
    {
        AssertDecodeRefused("FF 80", HpackDecodingError.TruncatedBlock);
    }

    [TestMethod]
    public void Decode_StringLengthAtLargestInteger_RefusesAsTruncatedBlock()
    {
        AssertDecodeRefused("00 7F 80 FF FF FF 07", HpackDecodingError.TruncatedBlock);
    }

    [TestMethod]
    public void Decode_LiteralWithNameButNoValue_RefusesAsTruncatedBlock()
    {
        AssertDecodeRefused("00 01 61", HpackDecodingError.TruncatedBlock);
    }

    [TestMethod]
    public void Decode_IndexZero_RefusesAsInvalidIndex()
    {
        AssertDecodeRefused("80", HpackDecodingError.InvalidIndex);
    }

    [TestMethod]
    public void Decode_IndexOnePastStaticTableWithEmptyDynamicTable_RefusesAsInvalidIndex()
    {
        AssertDecodeRefused("BE", HpackDecodingError.InvalidIndex);
    }

    [TestMethod]
    public void Decode_LiteralNameIndexOnePastStaticTableWithEmptyDynamicTable_RefusesAsInvalidIndex()
    {
        AssertDecodeRefused("7E 01 61", HpackDecodingError.InvalidIndex);
    }

    [TestMethod]
    public void Decode_LastStaticIndex_DecodesWwwAuthenticate()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("block", "BD");

        var fields = new HpackDecoder().Decode(FromHex("BD"));
        diagnostics.Act("fields", string.Join(", ", fields));

        diagnostics.Assert("field", new HeaderField("www-authenticate", string.Empty), fields.Single());
        AssertFields(fields, new HeaderField("www-authenticate", string.Empty));
    }

    [TestMethod]
    public void Decode_HuffmanValueHoldingEndOfString_RefusesAsEndOfStringInData()
    {
        AssertDecodeRefused("40 01 61 84 FF FF FF FF", HpackDecodingError.HuffmanEndOfStringInData);
    }

    // 'a' is 00011; 0x1F pads it with 111, and the extra FF makes the padding eleven bits.
    [TestMethod]
    public void Decode_HuffmanValuePaddedWithMoreThanSevenBits_RefusesAsInvalidPadding()
    {
        AssertDecodeRefused("40 01 61 82 1F FF", HpackDecodingError.InvalidHuffmanPadding);
    }

    [TestMethod]
    public void Decode_HuffmanValuePaddedWithAZeroBit_RefusesAsInvalidPadding()
    {
        AssertDecodeRefused("40 01 61 81 1E", HpackDecodingError.InvalidHuffmanPadding);
    }

    [TestMethod]
    public void Decode_EmptyHuffmanValue_DecodesAsEmptyString()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("block", "00 01 61 80");

        var fields = new HpackDecoder().Decode(FromHex("00 01 61 80"));
        diagnostics.Act("fields", string.Join(", ", fields));

        diagnostics.Assert("field", new HeaderField("a", string.Empty), fields.Single());
        AssertFields(fields, new HeaderField("a", string.Empty));
    }

    // "a  " codes to 5 + 6 + 6 = 17 bits, so its last octet carries exactly seven bits of padding.
    [TestMethod]
    public void HuffmanDecode_StringEndingInExactlySevenPaddingBits_Decodes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var text = "a  "u8.ToArray();
        var coded = HpackHuffman.Encode(text);
        diagnostics.Arrange("coded", Convert.ToHexString(coded));

        var decoded = HpackHuffman.Decode(coded);
        diagnostics.Act("decoded", Convert.ToHexString(decoded));

        diagnostics.Assert("coded length", 3, coded.Length);
        Assert.AreEqual(3, coded.Length);
        CollectionAssert.AreEqual(text, decoded);
    }

    [TestMethod]
    public void HuffmanDecode_ThirtyOneOneBits_RefusesAsEndOfStringInData()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("coded", "FF FF FF FE");

        var error = ErrorOf(() => HpackHuffman.Decode(FromHex("FF FF FF FE")));
        diagnostics.Act("error", error);

        diagnostics.Assert("error", HpackDecodingError.HuffmanEndOfStringInData, error);
        Assert.AreEqual(HpackDecodingError.HuffmanEndOfStringInData, error);
    }

    [TestMethod]
    public void Decode_TableSizeUpdateAfterAField_RefusesAsNotAtStart()
    {
        AssertDecodeRefused("88 20", HpackDecodingError.TableSizeUpdateNotAtStart);
    }

    // 3F E1 1F is 31 + 97 + 31 * 2^7 = 4096; 3F E2 1F is 4097.
    [TestMethod]
    public void Decode_TableSizeUpdateExactlyAtAllowedMaximum_IsAccepted()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new HpackDecoder();
        diagnostics.Arrange("block", "20 3F E1 1F 88");

        var fields = decoder.Decode(FromHex("20 3F E1 1F 88"));
        diagnostics.Act("maximum table size", decoder.MaximumTableSize);

        diagnostics.Assert("maximum table size", 4096, decoder.MaximumTableSize);
        Assert.AreEqual(4096, decoder.MaximumTableSize);
        AssertFields(fields, new HeaderField(":status", "200"));
    }

    [TestMethod]
    public void Decode_TableSizeUpdateOnePastAllowedMaximum_RefusesAsTooLarge()
    {
        AssertDecodeRefused("3F E2 1F", HpackDecodingError.TableSizeUpdateTooLarge);
    }

    [TestMethod]
    public void Decode_TableSizeUpdateAtLargestInteger_RefusesAsTooLarge()
    {
        AssertDecodeRefused("3F E0 FF FF FF 07", HpackDecodingError.TableSizeUpdateTooLarge);
    }

    [TestMethod]
    public void Decode_LoweredLimitThenBlockWithoutSizeUpdate_RefusesAsUpdateMissing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new HpackDecoder();
        decoder.SetAllowedMaximumTableSize(100);
        diagnostics.Arrange("allowed maximum", 100);

        var error = ErrorOf(() => decoder.Decode(FromHex("88")));
        diagnostics.Act("error", error);

        diagnostics.Assert("error", HpackDecodingError.TableSizeUpdateMissing, error);
        Assert.AreEqual(HpackDecodingError.TableSizeUpdateMissing, error);
    }

    [TestMethod]
    public void Decode_LoweredLimitThenSizeUpdateOnePastIt_RefusesAsTooLarge()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new HpackDecoder();
        decoder.SetAllowedMaximumTableSize(30);
        diagnostics.Arrange("allowed maximum", 30);

        var error = ErrorOf(() => decoder.Decode(FromHex("3F 00 88")));
        diagnostics.Act("error", error);

        diagnostics.Assert("error", HpackDecodingError.TableSizeUpdateTooLarge, error);
        Assert.AreEqual(HpackDecodingError.TableSizeUpdateTooLarge, error);
    }

    [TestMethod]
    public void Decode_LoweredLimitThenSizeUpdateExactlyToIt_DecodesAndShrinksTheTable()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new HpackDecoder();
        decoder.SetAllowedMaximumTableSize(30);
        diagnostics.Arrange("allowed maximum", 30);

        var fields = decoder.Decode(FromHex("3E 88"));
        diagnostics.Act("maximum table size", decoder.MaximumTableSize);

        diagnostics.Assert("maximum table size", 30, decoder.MaximumTableSize);
        Assert.AreEqual(30, decoder.MaximumTableSize);
        AssertFields(fields, new HeaderField(":status", "200"));
    }

    [TestMethod]
    public void Decode_IndexedLiteralLargerThanTheTable_EmptiesTheTableAndStillReturnsTheField()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new HpackDecoder(64);
        var value = new string('v', 40);
        byte[] block = [0x40, 0x01, (byte)'a', 0x01, (byte)'b', 0x40, 0x01, (byte)'a', 40, .. System.Text.Encoding.ASCII.GetBytes(value)];
        diagnostics.Arrange("block", Convert.ToHexString(block));

        var fields = decoder.Decode(block);
        diagnostics.Act("table size", decoder.TableSize);

        diagnostics.Assert("table size", 0, decoder.TableSize);
        Assert.AreEqual(0, decoder.TableSize);
        AssertFields(fields, new HeaderField("a", "b"), new HeaderField("a", value));
        Assert.AreEqual(HpackDecodingError.InvalidIndex, ErrorOf(() => decoder.Decode(FromHex("BE"))));
    }

    [TestMethod]
    public void Decode_EmptyBlock_ReturnsNoFields()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("block", "empty");

        var fields = new HpackDecoder().Decode([]);
        diagnostics.Act("field count", fields.Count);

        diagnostics.Assert("field count", 0, fields.Count);
        Assert.AreEqual(0, fields.Count);
    }

    [TestMethod]
    public void Decode_AfterARefusedBlock_DecodesTheNextBlockFromTheTableItHad()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new HpackDecoder();
        _ = decoder.Decode(FromHex("40 01 61 01 62"));
        diagnostics.Arrange("table size", decoder.TableSize);

        var error = ErrorOf(() => decoder.Decode(FromHex("BF")));
        var fields = decoder.Decode(FromHex("BE"));
        diagnostics.Act("error then fields", $"{error}; {string.Join(", ", fields)}");

        diagnostics.Assert("error", HpackDecodingError.InvalidIndex, error);
        Assert.AreEqual(HpackDecodingError.InvalidIndex, error);
        AssertFields(fields, new HeaderField("a", "b"));
    }

    [TestMethod]
    public void NewDecoder_NegativeTableSize_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("maximum table size", -1);

        var error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HpackDecoder(-1));
        diagnostics.Act("exception", error.GetType().Name);

        diagnostics.Assert("parameter", "maximumTableSize", error.ParamName);
        Assert.AreEqual("maximumTableSize", error.ParamName);
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HpackDecoder().SetAllowedMaximumTableSize(-1));
    }

    [TestMethod]
    public void NewEncoder_NegativeTableSize_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("maximum table size", -1);

        var error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HpackEncoder(-1));
        diagnostics.Act("exception", error.GetType().Name);

        diagnostics.Assert("parameter", "maximumTableSize", error.ParamName);
        Assert.AreEqual("maximumTableSize", error.ParamName);
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HpackEncoder().SetPeerMaximumTableSize(-1));
        _ = Assert.ThrowsExactly<ArgumentNullException>(() => new HpackEncoder().Encode(null!));
    }

    [TestMethod]
    public void Decode_EveryPrefixOfAValidBlock_DecodesOrRefusesWithHpackError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var block = new HpackEncoder().Encode(Fields((":status", "200"), ("content-type", "text/html; charset=utf-8"), ("x-custom", "a value Huffman shortens"), ("set-cookie", "id=1")));
        diagnostics.Bytes("block", block);

        var refusals = 0;
        for (var length = 0; length < block.Length; length++)
        {
            refusals += DecodesOrRefuses(new HpackDecoder(), block.AsSpan(0, length)) ? 0 : 1;
        }

        diagnostics.Act("refused prefixes", refusals);
        diagnostics.Assert("some prefixes refused", true, refusals > 0);
        Assert.IsTrue(refusals > 0);
    }

    [TestMethod]
    public void Decode_SeededSingleBitFlips_DecodeOrRefuseWithHpackError()
    {
        const int Seed = 1499;
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("seed", Seed);
        var random = new Random(Seed);
        var block = new HpackEncoder().Encode(Fields((":status", "404"), ("server", "nginx"), ("content-length", "1234"), ("x-trace", "abcdef0123456789")));

        var refusals = 0;
        for (var round = 0; round < 2000; round++)
        {
            var mutated = (byte[])block.Clone();
            mutated[random.Next(mutated.Length)] ^= (byte)(1 << random.Next(8));
            refusals += DecodesOrRefuses(new HpackDecoder(), mutated) ? 0 : 1;
        }

        diagnostics.Act("refused mutations", refusals);
        diagnostics.Assert("some mutations refused", true, refusals > 0);
        Assert.IsTrue(refusals > 0, $"seed {Seed}");
    }

    [TestMethod]
    public void Decode_SeededRandomBytes_DecodeOrRefuseWithHpackError()
    {
        const int Seed = 14991;
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("seed", Seed);
        var random = new Random(Seed);

        var refusals = 0;
        for (var round = 0; round < 2000; round++)
        {
            var bytes = new byte[random.Next(1, 40)];
            random.NextBytes(bytes);
            refusals += DecodesOrRefuses(new HpackDecoder(), bytes) ? 0 : 1;
        }

        diagnostics.Act("refused blocks", refusals);
        diagnostics.Assert("some blocks refused", true, refusals > 0);
        Assert.IsTrue(refusals > 0, $"seed {Seed}");
    }

    [TestMethod]
    public void EncodeThenDecode_SeededHeaderListsWhileTheTableSizeChanges_RoundTripEveryBlock()
    {
        const int Seed = 7541;
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("seed", Seed);
        var random = new Random(Seed);
        string[] names = [":status", "content-type", "set-cookie", "authorization", "x-a", "x-bb", "cookie", "date", "etag"];
        int[] sizes = [0, 1, 31, 32, 33, 64, 100, 256, 4096, 8192];
        var encoder = new HpackEncoder();
        var decoder = new HpackDecoder();

        for (var block = 0; block < 400; block++)
        {
            if (random.Next(10) == 0)
            {
                var size = sizes[random.Next(sizes.Length)];
                decoder.SetAllowedMaximumTableSize(size);
                encoder.SetPeerMaximumTableSize(size);
            }

            var sent = Enumerable.Range(0, random.Next(0, 12))
                .Select(_ => new HeaderField(names[random.Next(names.Length)], RandomValue(random)))
                .ToArray();
            var received = decoder.Decode(encoder.Encode(sent));

            CollectionAssert.AreEqual(
                sent.Select(field => (field.Name, field.Value)).ToArray(),
                received.Select(field => (field.Name, field.Value)).ToArray(),
                $"seed {Seed}, block {block}");
            Assert.AreEqual(encoder.TableSize, decoder.TableSize, $"seed {Seed}, block {block}");
        }

        diagnostics.Act("final table sizes", $"{encoder.TableSize} / {decoder.TableSize}");
        diagnostics.Assert("table sizes agree", encoder.TableSize, decoder.TableSize);
    }

    private static string RandomValue(Random random)
    {
        var characters = new char[random.Next(0, 80)];
        for (var index = 0; index < characters.Length; index++)
        {
            characters[index] = (char)random.Next(0x20, 0x7F);
        }

        return new string(characters);
    }

    /// <summary>Returns whether the block decoded; a refusal must be an <see cref="HpackDecodingException" />.</summary>
    private static bool DecodesOrRefuses(HpackDecoder decoder, ReadOnlySpan<byte> block)
    {
        try
        {
            _ = decoder.Decode(block);
            return true;
        }
        catch (HpackDecodingException)
        {
            return false;
        }
    }

    private void AssertDecodeRefused(string hex, HpackDecodingError expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("block", hex);

        var error = ErrorOf(() => new HpackDecoder().Decode(FromHex(hex)));
        diagnostics.Act("error", error);

        diagnostics.Assert("error", expected, error);
        Assert.AreEqual(expected, error);
    }
}
