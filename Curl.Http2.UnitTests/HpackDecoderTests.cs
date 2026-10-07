using Curl.Testing;
using static Curl.Http2.Hpack;

namespace Curl.Http2;

/// <summary>
/// Replays RFC 7541 appendix C.2, C.3, C.5 and C.6 through <see cref="HpackDecoder" />, and
/// checks each malformed block fails with its own <see cref="HpackDecodingError" />.
/// </summary>
[TestClass]
public sealed class HpackDecoderTests
{
    private const string Date21 = "Mon, 21 Oct 2013 20:13:21 GMT";
    private const string Date22 = "Mon, 21 Oct 2013 20:13:22 GMT";
    private const string Location = "https://www.example.com";
    private const string SetCookie = "foo=ASDJKHQKBZXOQWEOPIUAXQWEOIU; max-age=3600; version=1";

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // RFC 7541 appendix C.2.1.
    [TestMethod]
    public void Decode_C21LiteralWithIndexing_AddsTheFieldToTheTable()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new HpackDecoder();
        var block = FromHex("400a 6375 7374 6f6d 2d6b 6579 0d63 7573 746f 6d2d 6865 6164 6572");
        diagnostics.Bytes("block", block);
        diagnostics.Arrange("block length", block.Length);

        var fields = decoder.Decode(block);
        diagnostics.Act("decoded fields", string.Join(", ", fields));
        diagnostics.Act("table size", decoder.TableSize);

        diagnostics.Assert("table size", 55, decoder.TableSize);
        AssertFields(fields, new HeaderField("custom-key", "custom-header"));
        Assert.AreEqual(55, decoder.TableSize);
    }

    // RFC 7541 appendix C.2.2.
    [TestMethod]
    public void Decode_C22LiteralWithoutIndexing_LeavesTheTableEmpty()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new HpackDecoder();
        var block = FromHex("040c 2f73 616d 706c 652f 7061 7468");
        diagnostics.Bytes("block", block);
        diagnostics.Arrange("block length", block.Length);

        var fields = decoder.Decode(block);
        diagnostics.Act("decoded fields", string.Join(", ", fields));
        diagnostics.Act("table size", decoder.TableSize);

        diagnostics.Assert("table size", 0, decoder.TableSize);
        AssertFields(fields, new HeaderField(":path", "/sample/path"));
        Assert.AreEqual(0, decoder.TableSize);
    }

    // RFC 7541 appendix C.2.3.
    [TestMethod]
    public void Decode_C23LiteralNeverIndexed_MarksTheFieldNeverIndexed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new HpackDecoder();
        var block = FromHex("1008 7061 7373 776f 7264 0673 6563 7265 74");
        diagnostics.Bytes("block", block);
        diagnostics.Arrange("block length", block.Length);

        var fields = decoder.Decode(block);
        diagnostics.Act("decoded fields", string.Join(", ", fields));
        diagnostics.Act("table size", decoder.TableSize);

        diagnostics.Assert("table size", 0, decoder.TableSize);
        AssertFields(fields, new HeaderField("password", "secret", IsNeverIndexed: true));
        Assert.AreEqual(0, decoder.TableSize);
    }

    // RFC 7541 appendix C.2.4.
    [TestMethod]
    public void Decode_C24IndexedField_IsTheStaticEntry()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new HpackDecoder();
        var block = FromHex("82");
        diagnostics.Bytes("block", block);
        diagnostics.Arrange("block length", block.Length);

        var fields = decoder.Decode(block);
        diagnostics.Act("decoded fields", string.Join(", ", fields));
        diagnostics.Act("table size", decoder.TableSize);

        diagnostics.Assert("table size", 0, decoder.TableSize);
        AssertFields(fields, new HeaderField(":method", "GET"));
        Assert.AreEqual(0, decoder.TableSize);
    }

    // RFC 7541 appendix C.3.1, C.3.2 and C.3.3, on one decoder.
    [TestMethod]
    public void Decode_C3RequestsWithoutHuffman_GiveThePublishedListsAndTableSizes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("sequence", "RFC 7541 C.3 requests without Huffman");

        AssertRequestSequence(
            diagnostics,
            "8286 8441 0f77 7777 2e65 7861 6d70 6c65 2e63 6f6d",
            "8286 84be 5808 6e6f 2d63 6163 6865",
            "8287 85bf 400a 6375 7374 6f6d 2d6b 6579 0c63 7573 746f 6d2d 7661 6c75 65");
    }

    // RFC 7541 appendix C.4.1, C.4.2 and C.4.3, on one decoder.
    [TestMethod]
    public void Decode_C4RequestsWithHuffman_GiveThePublishedListsAndTableSizes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("sequence", "RFC 7541 C.4 requests with Huffman");

        AssertRequestSequence(
            diagnostics,
            "8286 8441 8cf1 e3c2 e5f2 3a6b a0ab 90f4 ff",
            "8286 84be 5886 a8eb 1064 9cbf",
            "8287 85bf 4088 25a8 49e9 5ba9 7d7f 8925 a849 e95b b8e8 b4bf");
    }

    // RFC 7541 appendix C.5.1, C.5.2 and C.5.3, on one decoder with a 256-byte table.
    [TestMethod]
    public void Decode_C5ResponsesWithoutHuffman_GiveThePublishedListsAndEvictions()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("sequence", "RFC 7541 C.5 responses without Huffman, 256-byte table");

        AssertResponseSequence(
            diagnostics,
            "4803 3330 3258 0770 7269 7661 7465 611d 4d6f 6e2c 2032 3120 4f63 7420 3230 3133 2032 303a 3133 3a32 3120 474d 546e 1768 7474 7073 3a2f 2f77 7777 2e65 7861 6d70 6c65 2e63 6f6d",
            "4803 3330 37c1 c0bf",
            "88c1 611d 4d6f 6e2c 2032 3120 4f63 7420 3230 3133 2032 303a 3133 3a32 3220 474d 54c0 5a04 677a 6970 7738 666f 6f3d 4153 444a 4b48 514b 425a 584f 5157 454f 5049 5541 5851 5745 4f49 553b 206d 6178 2d61 6765 3d33 3630 303b 2076 6572 7369 6f6e 3d31");
    }

    // RFC 7541 appendix C.6.1, C.6.2 and C.6.3, on one decoder with a 256-byte table.
    [TestMethod]
    public void Decode_C6ResponsesWithHuffman_GiveThePublishedListsAndEvictions()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("sequence", "RFC 7541 C.6 responses with Huffman, 256-byte table");

        AssertResponseSequence(
            diagnostics,
            "4882 6402 5885 aec3 771a 4b61 96d0 7abe 9410 54d4 44a8 2005 9504 0b81 66e0 82a6 2d1b ff6e 919d 29ad 1718 63c7 8f0b 97c8 e9ae 82ae 43d3",
            "4883 640e ffc1 c0bf",
            "88c1 6196 d07a be94 1054 d444 a820 0595 040b 8166 e084 a62d 1bff c05a 839b d9ab 77ad 94e7 821d d7f2 e6c7 b335 dfdf cd5b 3960 d5af 2708 7f36 72c1 ab27 0fb5 291f 9587 3160 65c0 03ed 4ee5 b106 3d50 07");
    }

    [TestMethod]
    public void Decode_IndexZero_FailsAsInvalidIndex()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] block = [0x80];
        diagnostics.Bytes("block", block);
        diagnostics.Arrange("block length", block.Length);

        var error = ErrorOf(() => new HpackDecoder().Decode([0x80]));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", HpackDecodingError.InvalidIndex, error);
        Assert.AreEqual(HpackDecodingError.InvalidIndex, ErrorOf(() => new HpackDecoder().Decode([0x80])));
    }

    [TestMethod]
    public void Decode_IndexBeyondTheDynamicTable_FailsAsInvalidIndex()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] block = [0xBE];
        diagnostics.Bytes("block", block);
        diagnostics.Arrange("block length", block.Length);

        var error = ErrorOf(() => new HpackDecoder().Decode([0xBE]));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", HpackDecodingError.InvalidIndex, error);
        Assert.AreEqual(HpackDecodingError.InvalidIndex, ErrorOf(() => new HpackDecoder().Decode([0xBE])));
    }

    [TestMethod]
    public void Decode_LiteralNameIndexBeyondTheTables_FailsAsInvalidIndex()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] block = [0x0F, 0x30, 0x00];
        diagnostics.Bytes("block", block);
        diagnostics.Arrange("block length", block.Length);

        var error = ErrorOf(() => new HpackDecoder().Decode([0x0F, 0x30, 0x00]));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", HpackDecodingError.InvalidIndex, error);
        Assert.AreEqual(HpackDecodingError.InvalidIndex, ErrorOf(() => new HpackDecoder().Decode([0x0F, 0x30, 0x00])));
    }

    [TestMethod]
    public void Decode_TableSizeUpdateAtTheLimit_ResizesTheTable()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new HpackDecoder(256);
        decoder.Decode(FromHex("400a 6375 7374 6f6d 2d6b 6579 0d63 7573 746f 6d2d 6865 6164 6572"));
        var block = FromHex("20 3f e1 01 82");
        diagnostics.Arrange("table size before", decoder.TableSize);
        diagnostics.Bytes("block", block);

        var fields = decoder.Decode(block);
        diagnostics.Act("decoded fields", string.Join(", ", fields));
        diagnostics.Act("maximum table size", decoder.MaximumTableSize);
        diagnostics.Act("table size", decoder.TableSize);

        diagnostics.Assert("maximum table size", 256, decoder.MaximumTableSize);
        diagnostics.Assert("table size", 0, decoder.TableSize);
        AssertFields(fields, new HeaderField(":method", "GET"));

        Assert.AreEqual(256, decoder.MaximumTableSize);
        Assert.AreEqual(0, decoder.TableSize);
    }

    [TestMethod]
    public void Decode_TableSizeUpdateAboveTheLimit_FailsAsTooLarge()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var block = FromHex("3f e2 1f");
        diagnostics.Bytes("block", block);
        diagnostics.Arrange("block length", block.Length);

        var error = ErrorOf(() => new HpackDecoder().Decode(FromHex("3f e2 1f")));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", HpackDecodingError.TableSizeUpdateTooLarge, error);
        Assert.AreEqual(
            HpackDecodingError.TableSizeUpdateTooLarge,
            ErrorOf(() => new HpackDecoder().Decode(FromHex("3f e2 1f"))));
    }

    [TestMethod]
    public void Decode_TableSizeUpdateAfterAField_FailsAsNotAtStart()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var block = FromHex("82 20");
        diagnostics.Bytes("block", block);
        diagnostics.Arrange("block length", block.Length);

        var error = ErrorOf(() => new HpackDecoder().Decode(FromHex("82 20")));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", HpackDecodingError.TableSizeUpdateNotAtStart, error);
        Assert.AreEqual(
            HpackDecodingError.TableSizeUpdateNotAtStart,
            ErrorOf(() => new HpackDecoder().Decode(FromHex("82 20"))));
    }

    [TestMethod]
    public void Decode_LimitLoweredAndNoUpdate_FailsAsUpdateMissing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new HpackDecoder();
        decoder.SetAllowedMaximumTableSize(100);
        var block = FromHex("82");
        diagnostics.Arrange("allowed maximum table size", 100);
        diagnostics.Bytes("block", block);

        var error = ErrorOf(() => decoder.Decode(FromHex("82")));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", HpackDecodingError.TableSizeUpdateMissing, error);
        Assert.AreEqual(HpackDecodingError.TableSizeUpdateMissing, ErrorOf(() => decoder.Decode(FromHex("82"))));
    }

    [TestMethod]
    public void Decode_LimitLoweredThenUpdated_DecodesAndNeedsNoFurtherUpdate()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new HpackDecoder();
        decoder.SetAllowedMaximumTableSize(100);
        var updated = FromHex("3f 13 82");
        var plain = FromHex("82");
        diagnostics.Arrange("allowed maximum table size", 100);
        diagnostics.Bytes("block with update", updated);
        diagnostics.Bytes("block without update", plain);

        var first = decoder.Decode(updated);
        var second = decoder.Decode(plain);
        diagnostics.Act("fields after update", string.Join(", ", first));
        diagnostics.Act("fields without update", string.Join(", ", second));
        diagnostics.Act("maximum table size", decoder.MaximumTableSize);

        diagnostics.Assert("maximum table size", 50, decoder.MaximumTableSize);
        AssertFields(first, new HeaderField(":method", "GET"));
        AssertFields(second, new HeaderField(":method", "GET"));
        Assert.AreEqual(50, decoder.MaximumTableSize);
    }

    [TestMethod]
    public void Decode_LimitRaised_NeedsNoUpdate()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new HpackDecoder();
        decoder.SetAllowedMaximumTableSize(8192);
        var block = FromHex("82");
        diagnostics.Arrange("allowed maximum table size", 8192);
        diagnostics.Bytes("block", block);

        var fields = decoder.Decode(block);
        diagnostics.Act("decoded fields", string.Join(", ", fields));
        diagnostics.Act("maximum table size", decoder.MaximumTableSize);

        diagnostics.Assert("maximum table size", 4096, decoder.MaximumTableSize);
        AssertFields(fields, new HeaderField(":method", "GET"));
        Assert.AreEqual(4096, decoder.MaximumTableSize);
    }

    [TestMethod]
    public void Decode_BadHuffmanPaddingInAValue_FailsAsInvalidPadding()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var block = FromHex("01 81 00");
        diagnostics.Bytes("block", block);
        diagnostics.Arrange("block length", block.Length);

        var error = ErrorOf(() => new HpackDecoder().Decode(FromHex("01 81 00")));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", HpackDecodingError.InvalidHuffmanPadding, error);
        Assert.AreEqual(
            HpackDecodingError.InvalidHuffmanPadding,
            ErrorOf(() => new HpackDecoder().Decode(FromHex("01 81 00"))));
    }

    [TestMethod]
    public void Decode_LiteralWithoutItsName_FailsAsTruncatedBlock()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] block = [0x40];
        diagnostics.Bytes("block", block);
        diagnostics.Arrange("block length", block.Length);

        var error = ErrorOf(() => new HpackDecoder().Decode([0x40]));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", HpackDecodingError.TruncatedBlock, error);
        Assert.AreEqual(HpackDecodingError.TruncatedBlock, ErrorOf(() => new HpackDecoder().Decode([0x40])));
    }

    [TestMethod]
    public void Decode_EmptyBlock_IsAnEmptyList()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("block", "empty");

        var fields = new HpackDecoder().Decode([]);
        diagnostics.Act("decoded fields", string.Join(", ", fields));

        diagnostics.Assert("field count", 0, fields.Count);
        Assert.IsEmpty(new HpackDecoder().Decode([]));
    }

    [TestMethod]
    public void Constructor_NegativeTableSize_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("table size", -1);

        var error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HpackDecoder(-1));
        diagnostics.Act("constructing new HpackDecoder(-1) threw", error.GetType().Name);

        diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), error.GetType().Name);
    }

    [TestMethod]
    public void SetAllowedMaximumTableSize_Negative_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("allowed maximum table size", -1);

        var error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HpackDecoder().SetAllowedMaximumTableSize(-1));
        diagnostics.Act("SetAllowedMaximumTableSize(-1) threw", error.GetType().Name);

        diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), error.GetType().Name);
    }

    private static void AssertRequestSequence(TestDiagnostics diagnostics, string first, string second, string third)
    {
        var decoder = new HpackDecoder();
        diagnostics.Bytes("first block", FromHex(first));
        diagnostics.Bytes("second block", FromHex(second));
        diagnostics.Bytes("third block", FromHex(third));

        var firstFields = decoder.Decode(FromHex(first));
        diagnostics.Act("first fields", string.Join(", ", firstFields));
        diagnostics.Assert("first table size", 57, decoder.TableSize);
        AssertFields(
            firstFields,
            Fields((":method", "GET"), (":scheme", "http"), (":path", "/"), (":authority", "www.example.com")));
        Assert.AreEqual(57, decoder.TableSize);

        var secondFields = decoder.Decode(FromHex(second));
        diagnostics.Act("second fields", string.Join(", ", secondFields));
        diagnostics.Assert("second table size", 110, decoder.TableSize);
        AssertFields(
            secondFields,
            Fields((":method", "GET"), (":scheme", "http"), (":path", "/"), (":authority", "www.example.com"), ("cache-control", "no-cache")));
        Assert.AreEqual(110, decoder.TableSize);

        var thirdFields = decoder.Decode(FromHex(third));
        diagnostics.Act("third fields", string.Join(", ", thirdFields));
        diagnostics.Assert("third table size", 164, decoder.TableSize);
        AssertFields(
            thirdFields,
            Fields((":method", "GET"), (":scheme", "https"), (":path", "/index.html"), (":authority", "www.example.com"), ("custom-key", "custom-value")));
        Assert.AreEqual(164, decoder.TableSize);
    }

    private static void AssertResponseSequence(TestDiagnostics diagnostics, string first, string second, string third)
    {
        var decoder = new HpackDecoder(256);
        diagnostics.Bytes("first block", FromHex(first));
        diagnostics.Bytes("second block", FromHex(second));
        diagnostics.Bytes("third block", FromHex(third));

        var firstFields = decoder.Decode(FromHex(first));
        diagnostics.Act("first fields", string.Join(", ", firstFields));
        diagnostics.Assert("first table size", 222, decoder.TableSize);
        AssertFields(
            firstFields,
            Fields((":status", "302"), ("cache-control", "private"), ("date", Date21), ("location", Location)));
        Assert.AreEqual(222, decoder.TableSize);

        var secondFields = decoder.Decode(FromHex(second));
        diagnostics.Act("second fields", string.Join(", ", secondFields));
        diagnostics.Assert("second table size", 222, decoder.TableSize);
        AssertFields(
            secondFields,
            Fields((":status", "307"), ("cache-control", "private"), ("date", Date21), ("location", Location)));
        Assert.AreEqual(222, decoder.TableSize);

        var thirdFields = decoder.Decode(FromHex(third));
        diagnostics.Act("third fields", string.Join(", ", thirdFields));
        diagnostics.Assert("third table size", 215, decoder.TableSize);
        AssertFields(
            thirdFields,
            Fields((":status", "200"), ("cache-control", "private"), ("date", Date22), ("location", Location), ("content-encoding", "gzip"), ("set-cookie", SetCookie)));
        Assert.AreEqual(215, decoder.TableSize);
    }
}
