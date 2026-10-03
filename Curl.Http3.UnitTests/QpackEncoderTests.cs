using Curl.Http2;
using static Curl.Http3.Qpack;

namespace Curl.Http3;

/// <summary>
/// Pins <see cref="QpackEncoder" />'s choice of representation, its refusal to evict an
/// entry the decoder may still need (RFC 9204 section 2.1.1), its blocked-streams limit
/// (section 2.1.2) and its reading of the decoder stream (section 4.4).
/// </summary>
[TestClass]
public sealed class QpackEncoderTests
{
    [TestMethod]
    public void Constructor_NegativeLimits_AreRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QpackEncoder(-1, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QpackEncoder(0, -1));
    }

    [TestMethod]
    public void EncodeFieldSection_NullFields_IsRejected() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new QpackEncoder(0, 0).EncodeFieldSection(0, null!));

    [TestMethod]
    public void EncodeFieldSection_WithoutADynamicTable_UsesStaticIndexesAndLiterals()
    {
        var encoder = new QpackEncoder(0, 0, huffmanCodeLiterals: false);
        HeaderField[] fields =
        [
            new(":method", "GET"),
            new(":path", "/", IsNeverIndexed: true),
            new("ab", "c", IsNeverIndexed: true),
            new("ab", "c"),
        ];

        var section = encoder.EncodeFieldSection(0, fields);

        CollectionAssert.AreEqual(FromHex("0000 d1 71012f 32616201 63 22616201 63"), section);
        Assert.IsEmpty(encoder.TakeEncoderStreamBytes());
        Assert.AreEqual(0, encoder.DynamicTableCapacity);
    }

    [TestMethod]
    public void EncodeFieldSection_ByDefault_HuffmanCodesLiteralsThatShrink() =>
        CollectionAssert.AreEqual(
            FromHex("0000 2f01 25a849e95ba97d7f 89 25a849e95bb8e8b4bf"),
            new QpackEncoder(0, 0).EncodeFieldSection(0, Fields(("custom-key", "custom-value"))));

    [TestMethod]
    public void EncodeFieldSection_StreamMayNotBlock_ReferencesOnlyAcknowledgedEntries()
    {
        var encoder = new QpackEncoder(220, 0, huffmanCodeLiterals: false);
        encoder.TrySetDynamicTableCapacity(220);
        encoder.TryInsert(new HeaderField("custom-key", "custom-value"));
        encoder.TryInsert(new HeaderField("x", "1"));
        encoder.ReadDecoderStream(FromHex("01"));

        var section = encoder.EncodeFieldSection(4, Fields(("custom-key", "custom-value"), ("custom-key", "other"), ("x", "1")));

        CollectionAssert.AreEqual(FromHex("0201 81 41 056f74686572 21 78 0131"), section);
        Assert.AreEqual(2, encoder.InsertCount);
    }

    [TestMethod]
    public void EncodeFieldSection_EntryTooLargeToInsert_NamesTheEntryInsertedForThisSectionPostBase()
    {
        var encoder = new QpackEncoder(100, 1, huffmanCodeLiterals: false, tryIndexEveryName: true);
        encoder.TrySetDynamicTableCapacity(100);
        var longValue = new string('v', 70);

        var section = encoder.EncodeFieldSection(4, Fields(("x-a", "1"), ("x-a", longValue)));

        CollectionAssert.AreEqual(FromHex("0280 10 00 46").Concat(Enumerable.Repeat((byte)'v', 70)).ToArray(), section);
        CollectionAssert.AreEqual(FromHex("3f45 43782d61 0131"), encoder.TakeEncoderStreamBytes());
    }

    [TestMethod]
    public void EncodeFieldSection_BlockedStreamLimit_LetsABlockedStreamGoOnBlockingButNoOther()
    {
        var encoder = new QpackEncoder(220, 1, huffmanCodeLiterals: false, tryIndexEveryName: true);
        encoder.TrySetDynamicTableCapacity(220);

        CollectionAssert.AreEqual(FromHex("0280 10"), encoder.EncodeFieldSection(4, Fields(("x", "1"))));
        CollectionAssert.AreEqual(FromHex("0380 10"), encoder.EncodeFieldSection(4, Fields(("y", "2"))));
        // Nothing on it references the dynamic table, so its Base is 0 too, not the Insert Count.
        CollectionAssert.AreEqual(FromHex("0000 21 7a 0133"), encoder.EncodeFieldSection(8, Fields(("z", "3"))));

        encoder.ReadDecoderStream(FromHex("02"));

        CollectionAssert.AreEqual(FromHex("0480 10"), encoder.EncodeFieldSection(8, Fields(("z", "3"))));
    }

    [TestMethod]
    public void TryInsert_WouldEvictAnEntryAnUnacknowledgedSectionReferences_IsRefusedUntilAcknowledged()
    {
        var encoder = new QpackEncoder(100, 2, huffmanCodeLiterals: false, tryIndexEveryName: true);
        encoder.TrySetDynamicTableCapacity(100);
        encoder.EncodeFieldSection(4, Fields(("x-a", "1")));
        encoder.ReadDecoderStream(FromHex("01"));
        var large = new HeaderField("x-b", new string('v', 60));

        Assert.IsFalse(encoder.TryInsert(large));

        encoder.ReadDecoderStream(FromHex("84"));
        Assert.IsTrue(encoder.TryInsert(large));
        Assert.AreEqual(95, encoder.DynamicTableSize);
    }

    [TestMethod]
    public void TryInsert_StreamCancelled_ReleasesItsReferences()
    {
        var encoder = new QpackEncoder(100, 2, huffmanCodeLiterals: false, tryIndexEveryName: true);
        encoder.TrySetDynamicTableCapacity(100);
        encoder.EncodeFieldSection(4, Fields(("x-a", "1")));
        encoder.ReadDecoderStream(FromHex("01"));

        encoder.ReadDecoderStream(FromHex("44"));

        Assert.IsTrue(encoder.TryInsert(new HeaderField("x-b", new string('v', 60))));
    }

    [TestMethod]
    public void TryInsert_WithNoCapacity_IsRefused() =>
        Assert.IsFalse(new QpackEncoder(100, 0).TryInsert(new HeaderField("a", "1")));

    [TestMethod]
    public void TrySetDynamicTableCapacity_OutOfRange_IsRejected()
    {
        var encoder = new QpackEncoder(100, 0);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => encoder.TrySetDynamicTableCapacity(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => encoder.TrySetDynamicTableCapacity(101));
    }

    [TestMethod]
    public void TrySetDynamicTableCapacity_ShrinkingWouldEvictAnUnacknowledgedEntry_IsRefusedUntilAcknowledged()
    {
        var encoder = new QpackEncoder(100, 0);
        encoder.TrySetDynamicTableCapacity(100);
        encoder.TryInsert(new HeaderField("a", "1"));
        encoder.TakeEncoderStreamBytes();

        Assert.IsFalse(encoder.TrySetDynamicTableCapacity(0));
        Assert.IsEmpty(encoder.TakeEncoderStreamBytes());

        encoder.ReadDecoderStream(FromHex("01"));
        Assert.IsTrue(encoder.TrySetDynamicTableCapacity(0));
        CollectionAssert.AreEqual(FromHex("20"), encoder.TakeEncoderStreamBytes());
        Assert.AreEqual(0, encoder.DynamicTableSize);
    }

    [TestMethod]
    public void TryDuplicate_NoSuchEntry_IsRejected() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QpackEncoder(100, 0).TryDuplicate(0));

    [TestMethod]
    public void TryDuplicate_CopyWouldEvictAnUnacknowledgedEntry_IsRefused()
    {
        var encoder = new QpackEncoder(100, 0);
        encoder.TrySetDynamicTableCapacity(64);
        encoder.TryInsert(new HeaderField("a", "1"));

        Assert.IsFalse(encoder.TryDuplicate(0));
        Assert.AreEqual(1, encoder.InsertCount);
    }

    [TestMethod]
    [DataRow("80")]
    [DataRow("00")]
    [DataRow("01")]
    public void ReadDecoderStream_InvalidInstruction_FailsAsDecoderStreamError(string bytes) =>
        Assert.AreEqual(QpackErrorCode.DecoderStreamError, ErrorOf(() => new QpackEncoder(100, 0).ReadDecoderStream(FromHex(bytes))));

    [TestMethod]
    public void ReadDecoderStream_EndlessIntegerContinuation_FailsAsDecoderStreamErrorWithinTenBytes()
    {
        var encoder = new QpackEncoder(100, 0);
        encoder.ReadDecoderStream(FromHex("ff 80808080 80808080"));

        Assert.AreEqual(QpackErrorCode.DecoderStreamError, ErrorOf(() => encoder.ReadDecoderStream(FromHex("80"))));
    }

    [TestMethod]
    public void ReadDecoderStream_InstructionSplitAcrossReads_TakesEffectOnceComplete()
    {
        var encoder = new QpackEncoder(220, 1, huffmanCodeLiterals: false, tryIndexEveryName: true);
        encoder.TrySetDynamicTableCapacity(220);
        encoder.EncodeFieldSection(200, Fields(("x", "1")));

        encoder.ReadDecoderStream(FromHex("ff"));
        Assert.AreEqual(0, encoder.KnownReceivedCount);
        encoder.ReadDecoderStream(FromHex("49"));

        Assert.AreEqual(1, encoder.KnownReceivedCount);
    }

    [TestMethod]
    [DataRow(0L, "authorization", "Basic dTpw", "0000 7f45 0a 42617369632064547077")]
    [DataRow(4096L, "authorization", "Basic dTpw", "0000 7f45 0a 42617369632064547077")]
    [DataRow(0L, "cookie", "a=b", "0000 75 03 613d62")]
    [DataRow(4096L, "cookie", "a=b", "0000 75 03 613d62")]
    public void EncodeFieldSection_AuthorizationOrShortCookie_IsANeverIndexedLiteralNamingTheStaticEntry(
        long capacity, string name, string value, string expectedSection)
    {
        var encoder = new QpackEncoder(capacity, 16, huffmanCodeLiterals: false);
        encoder.TrySetDynamicTableCapacity(capacity);
        var decoder = new QpackDecoder(capacity, 16);
        decoder.ReadEncoderStream(encoder.TakeEncoderStreamBytes());

        var section = encoder.EncodeFieldSection(0, Fields((name, value)));

        CollectionAssert.AreEqual(FromHex(expectedSection), section);
        Assert.IsEmpty(encoder.TakeEncoderStreamBytes());
        Assert.AreEqual(0, encoder.InsertCount);
        CollectionAssert.AreEqual(new HeaderField[] { new(name, value, IsNeverIndexed: true) }, Decode(decoder, 0, section));
    }

    [TestMethod]
    [DataRow("authorization", "Basic dTpw", "0000 7f45 0a 42617369632064547077")]
    [DataRow("cookie", "a=b", "0000 75 03 613d62")]
    public void EncodeFieldSection_AuthorizationOrShortCookieAlreadyInTheDynamicTable_IsNeverReferencedFromIt(
        string name, string value, string expectedSection)
    {
        var encoder = new QpackEncoder(4096, 16, huffmanCodeLiterals: false);
        encoder.TrySetDynamicTableCapacity(4096);
        encoder.TryInsert(new HeaderField(name, value));
        encoder.TakeEncoderStreamBytes();

        var section = encoder.EncodeFieldSection(0, Fields((name, value)));

        CollectionAssert.AreEqual(FromHex(expectedSection), section);
        Assert.IsEmpty(encoder.TakeEncoderStreamBytes());
    }

    [TestMethod]
    [DataRow(":path", "/x", "0000 51 022f78")]
    [DataRow("etag", "\"a\"", "0000 57 03226122")]
    [DataRow("x-custom", "1", "0000 2701 782d637573746f6d 0131")]
    public void EncodeFieldSection_NameNghttp3KeepsLiteral_IsNeverInserted(string name, string value, string expectedSection)
    {
        var encoder = new QpackEncoder(4096, 16, huffmanCodeLiterals: false);
        encoder.TrySetDynamicTableCapacity(4096);
        encoder.TakeEncoderStreamBytes();

        var section = encoder.EncodeFieldSection(0, Fields((name, value)));

        CollectionAssert.AreEqual(FromHex(expectedSection), section);
        Assert.IsEmpty(encoder.TakeEncoderStreamBytes());
        Assert.AreEqual(0, encoder.InsertCount);
    }

    [TestMethod]
    public void EncodeFieldSection_FieldOverThreeQuartersOfTheCapacity_IsNeverInserted()
    {
        var encoder = new QpackEncoder(100, 16, huffmanCodeLiterals: false, tryIndexEveryName: true);
        encoder.TrySetDynamicTableCapacity(100);
        encoder.TakeEncoderStreamBytes();
        var value = new string('v', 34);

        var section = encoder.EncodeFieldSection(0, Fields(("user-agent", value)));

        CollectionAssert.AreEqual(FromHex("0000 5f50 22").Concat(Enumerable.Repeat((byte)'v', 34)).ToArray(), section);
        Assert.IsEmpty(encoder.TakeEncoderStreamBytes());
    }

    [TestMethod]
    [DataRow("user-agent", "curl/8.21.0", "ff20 0b 6375726c2f382e32312e30")]
    [DataRow("host", "a", "44 686f7374 01 61")]
    public void EncodeFieldSection_NameNghttp3Stores_IsInserted(string name, string value, string expectedEncoderStream)
    {
        var encoder = new QpackEncoder(4096, 16, huffmanCodeLiterals: false);
        encoder.TrySetDynamicTableCapacity(4096);
        encoder.TakeEncoderStreamBytes();

        var section = encoder.EncodeFieldSection(0, Fields((name, value)));

        CollectionAssert.AreEqual(FromHex("0280 10"), section);
        CollectionAssert.AreEqual(FromHex(expectedEncoderStream), encoder.TakeEncoderStreamBytes());
    }

    [TestMethod]
    public void EncodeFieldSection_LiteralOnlyFieldAlreadyInTheDynamicTable_IsReferencedFromIt()
    {
        var encoder = new QpackEncoder(4096, 16, huffmanCodeLiterals: false);
        encoder.TrySetDynamicTableCapacity(4096);
        encoder.TryInsert(new HeaderField(":path", "/x"));
        encoder.TakeEncoderStreamBytes();

        var section = encoder.EncodeFieldSection(0, Fields((":path", "/x")));

        CollectionAssert.AreEqual(FromHex("0200 80"), section);
        Assert.IsEmpty(encoder.TakeEncoderStreamBytes());
    }

    [TestMethod]
    public void EncodeFieldSection_CookieOfTwentyBytes_StaysIndexable()
    {
        const string Value = "a=0123456789abcdefgh";
        var withoutTable = new QpackEncoder(0, 0, huffmanCodeLiterals: false);
        var withTable = new QpackEncoder(4096, 16, huffmanCodeLiterals: false);
        withTable.TrySetDynamicTableCapacity(4096);
        withTable.TakeEncoderStreamBytes();

        CollectionAssert.AreEqual(
            FromHex("0000 55 14 613d3031323334353637383961626364656667 68"),
            withoutTable.EncodeFieldSection(0, Fields(("cookie", Value))));
        CollectionAssert.AreEqual(FromHex("0280 10"), withTable.EncodeFieldSection(0, Fields(("cookie", Value))));
        CollectionAssert.AreEqual(FromHex("c5 14 613d3031323334353637383961626364656667 68"), withTable.TakeEncoderStreamBytes());
    }
}
