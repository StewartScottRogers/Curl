using Curl.Http2;
using Curl.Testing;
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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Constructor_NegativeLimits_AreRejected()
    {
        Diagnostics.Arrange("limits", "capacity -1; blocked streams -1");

        var capacity = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QpackEncoder(-1, 0));
        var blocked = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QpackEncoder(0, -1));

        Diagnostics.Act("parameters", $"{capacity.ParamName}, {blocked.ParamName}");
        Diagnostics.Assert("exceptions", "ArgumentOutOfRangeException x2", "as expected");
    }

    [TestMethod]
    public void EncodeFieldSection_NullFields_IsRejected()
    {
        Diagnostics.Arrange("fields", "null");

        var failure = Assert.ThrowsExactly<ArgumentNullException>(() => new QpackEncoder(0, 0).EncodeFieldSection(0, null!));

        Diagnostics.Act("parameter", failure.ParamName);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), failure.GetType().Name);
    }

    [TestMethod]
    public void EncodeFieldSection_WithoutADynamicTable_UsesStaticIndexesAndLiterals()
    {
        var encoder = new QpackEncoder(0, 0, huffmanCodeLiterals: false);
        Diagnostics.Arrange("encoder", "0, 0, huffmanCodeLiterals: false");
        HeaderField[] fields =
        [
            new(":method", "GET"),
            new(":path", "/", IsNeverIndexed: true),
            new("ab", "c", IsNeverIndexed: true),
            new("ab", "c"),
        ];

        var section = encoder.EncodeFieldSection(0, fields);

        Diagnostics.Act("section length", section.Length);
        Diagnostics.Bytes("section", section);
        Diagnostics.Diff("section", FromHex("0000 d1 71012f 32616201 63 22616201 63"), section);
        CollectionAssert.AreEqual(FromHex("0000 d1 71012f 32616201 63 22616201 63"), section);
        Assert.IsEmpty(encoder.TakeEncoderStreamBytes());
        Assert.AreEqual(0, encoder.DynamicTableCapacity);
    }

    [TestMethod]
    public void EncodeFieldSection_ByDefault_HuffmanCodesLiteralsThatShrink()
    {
        Diagnostics.Arrange("fields", "custom-key: custom-value, default encoder (0, 0)");
        var section = new QpackEncoder(0, 0).EncodeFieldSection(0, Fields(("custom-key", "custom-value")));
        Diagnostics.Act("section length", section.Length);
        Diagnostics.Bytes("section", section);
        Diagnostics.Diff("section", FromHex("0000 2f01 25a849e95ba97d7f 89 25a849e95bb8e8b4bf"), section);

        CollectionAssert.AreEqual(
            FromHex("0000 2f01 25a849e95ba97d7f 89 25a849e95bb8e8b4bf"),
            section);
    }

    [TestMethod]
    public void EncodeFieldSection_StreamMayNotBlock_ReferencesOnlyAcknowledgedEntries()
    {
        var encoder = new QpackEncoder(220, 0, huffmanCodeLiterals: false);
        Diagnostics.Arrange("encoder", "220, 0, huffmanCodeLiterals: false");
        encoder.TrySetDynamicTableCapacity(220);
        encoder.TryInsert(new HeaderField("custom-key", "custom-value"));
        encoder.TryInsert(new HeaderField("x", "1"));
        encoder.ReadDecoderStream(FromHex("01"));

        var section = encoder.EncodeFieldSection(4, Fields(("custom-key", "custom-value"), ("custom-key", "other"), ("x", "1")));

        Diagnostics.Act("section length", section.Length);
        Diagnostics.Bytes("section", section);
        Diagnostics.Diff("section", FromHex("0201 81 41 056f74686572 21 78 0131"), section);
        CollectionAssert.AreEqual(FromHex("0201 81 41 056f74686572 21 78 0131"), section);
        Assert.AreEqual(2, encoder.InsertCount);
    }

    [TestMethod]
    public void EncodeFieldSection_EntryTooLargeToInsert_NamesTheEntryInsertedForThisSectionPostBase()
    {
        var encoder = new QpackEncoder(100, 1, huffmanCodeLiterals: false, tryIndexEveryName: true);
        Diagnostics.Arrange("encoder", "100, 1, huffmanCodeLiterals: false, tryIndexEveryName: true");
        encoder.TrySetDynamicTableCapacity(100);
        var longValue = new string('v', 70);

        var section = encoder.EncodeFieldSection(4, Fields(("x-a", "1"), ("x-a", longValue)));

        Diagnostics.Act("section length", section.Length);
        Diagnostics.Bytes("section", section);
        Diagnostics.Diff("section", FromHex("0280 10 00 46").Concat(Enumerable.Repeat((byte)'v', 70)).ToArray(), section);
        CollectionAssert.AreEqual(FromHex("0280 10 00 46").Concat(Enumerable.Repeat((byte)'v', 70)).ToArray(), section);
        CollectionAssert.AreEqual(FromHex("3f45 43782d61 0131"), encoder.TakeEncoderStreamBytes());
    }

    [TestMethod]
    public void EncodeFieldSection_BlockedStreamLimit_LetsABlockedStreamGoOnBlockingButNoOther()
    {
        var encoder = new QpackEncoder(220, 1, huffmanCodeLiterals: false, tryIndexEveryName: true);
        Diagnostics.Arrange("encoder", "220, 1, huffmanCodeLiterals: false, tryIndexEveryName: true");
        encoder.TrySetDynamicTableCapacity(220);

        CollectionAssert.AreEqual(FromHex("0280 10"), encoder.EncodeFieldSection(4, Fields(("x", "1"))));
        CollectionAssert.AreEqual(FromHex("0380 10"), encoder.EncodeFieldSection(4, Fields(("y", "2"))));
        // Nothing on it references the dynamic table, so its Base is 0 too, not the Insert Count.
        CollectionAssert.AreEqual(FromHex("0000 21 7a 0133"), encoder.EncodeFieldSection(8, Fields(("z", "3"))));

        encoder.ReadDecoderStream(FromHex("02"));

        var unblocked = encoder.EncodeFieldSection(8, Fields(("z", "3")));
        Diagnostics.Bytes("section after the acknowledgement", unblocked);
        Diagnostics.Act("insert count", encoder.InsertCount);
        Diagnostics.Diff("section after the acknowledgement", FromHex("0480 10"), unblocked);
        CollectionAssert.AreEqual(FromHex("0480 10"), unblocked);
    }

    [TestMethod]
    public void TryInsert_WouldEvictAnEntryAnUnacknowledgedSectionReferences_IsRefusedUntilAcknowledged()
    {
        var encoder = new QpackEncoder(100, 2, huffmanCodeLiterals: false, tryIndexEveryName: true);
        Diagnostics.Arrange("encoder", "100, 2, huffmanCodeLiterals: false, tryIndexEveryName: true");
        encoder.TrySetDynamicTableCapacity(100);
        encoder.EncodeFieldSection(4, Fields(("x-a", "1")));
        encoder.ReadDecoderStream(FromHex("01"));
        var large = new HeaderField("x-b", new string('v', 60));

        Assert.IsFalse(encoder.TryInsert(large));

        encoder.ReadDecoderStream(FromHex("84"));
        Assert.IsTrue(encoder.TryInsert(large));
        Diagnostics.Act("dynamic table size", encoder.DynamicTableSize);
        Diagnostics.Assert("dynamic table size", 95L, encoder.DynamicTableSize);
        Assert.AreEqual(95, encoder.DynamicTableSize);
    }

    [TestMethod]
    public void TryInsert_StreamCancelled_ReleasesItsReferences()
    {
        var encoder = new QpackEncoder(100, 2, huffmanCodeLiterals: false, tryIndexEveryName: true);
        Diagnostics.Arrange("encoder", "100, 2, huffmanCodeLiterals: false, tryIndexEveryName: true");
        encoder.TrySetDynamicTableCapacity(100);
        encoder.EncodeFieldSection(4, Fields(("x-a", "1")));
        encoder.ReadDecoderStream(FromHex("01"));

        encoder.ReadDecoderStream(FromHex("44"));

        var inserted = encoder.TryInsert(new HeaderField("x-b", new string('v', 60)));
        Diagnostics.Act("inserted", inserted);
        Diagnostics.Assert("inserted", true, inserted);
        Assert.IsTrue(inserted);
    }

    [TestMethod]
    public void TryInsert_WithNoCapacity_IsRefused()
    {
        Diagnostics.Arrange("encoder", "100, 0, no capacity set; inserting a: 1");
        var inserted = new QpackEncoder(100, 0).TryInsert(new HeaderField("a", "1"));
        Diagnostics.Act("inserted", inserted);
        Diagnostics.Assert("inserted", false, inserted);
        Assert.IsFalse(inserted);
    }

    [TestMethod]
    public void TrySetDynamicTableCapacity_OutOfRange_IsRejected()
    {
        var encoder = new QpackEncoder(100, 0);
        Diagnostics.Arrange("encoder", "100, 0");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => encoder.TrySetDynamicTableCapacity(-1));
        var tooLarge = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => encoder.TrySetDynamicTableCapacity(101));

        Diagnostics.Act("parameter", tooLarge.ParamName);
        Diagnostics.Assert("exceptions for -1 and 101", "ArgumentOutOfRangeException x2", "as expected");
    }

    [TestMethod]
    public void TrySetDynamicTableCapacity_ShrinkingWouldEvictAnUnacknowledgedEntry_IsRefusedUntilAcknowledged()
    {
        var encoder = new QpackEncoder(100, 0);
        Diagnostics.Arrange("encoder", "100, 0");
        encoder.TrySetDynamicTableCapacity(100);
        encoder.TryInsert(new HeaderField("a", "1"));
        encoder.TakeEncoderStreamBytes();

        Assert.IsFalse(encoder.TrySetDynamicTableCapacity(0));
        Assert.IsEmpty(encoder.TakeEncoderStreamBytes());

        encoder.ReadDecoderStream(FromHex("01"));
        Assert.IsTrue(encoder.TrySetDynamicTableCapacity(0));
        CollectionAssert.AreEqual(FromHex("20"), encoder.TakeEncoderStreamBytes());
        Diagnostics.Act("dynamic table size", encoder.DynamicTableSize);
        Diagnostics.Assert("dynamic table size", 0L, encoder.DynamicTableSize);
        Assert.AreEqual(0, encoder.DynamicTableSize);
    }

    [TestMethod]
    public void TryDuplicate_NoSuchEntry_IsRejected()
    {
        Diagnostics.Arrange("encoder", "100, 0, empty table; duplicating entry 0");

        var failure = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QpackEncoder(100, 0).TryDuplicate(0));

        Diagnostics.Act("parameter", failure.ParamName);
        Diagnostics.Assert("exception", nameof(ArgumentOutOfRangeException), failure.GetType().Name);
    }

    [TestMethod]
    public void TryDuplicate_CopyWouldEvictAnUnacknowledgedEntry_IsRefused()
    {
        var encoder = new QpackEncoder(100, 0);
        Diagnostics.Arrange("encoder", "100, 0");
        encoder.TrySetDynamicTableCapacity(64);
        encoder.TryInsert(new HeaderField("a", "1"));

        var duplicated = encoder.TryDuplicate(0);
        Diagnostics.Act("duplicated", duplicated);
        Diagnostics.Assert("insert count", 1L, encoder.InsertCount);
        Assert.IsFalse(duplicated);
        Assert.AreEqual(1, encoder.InsertCount);
    }

    [TestMethod]
    [DataRow("80")]
    [DataRow("00")]
    [DataRow("01")]
    public void ReadDecoderStream_InvalidInstruction_FailsAsDecoderStreamError(string bytes)
    {
        Diagnostics.Arrange("decoder stream", bytes);

        Assert.AreEqual(QpackErrorCode.DecoderStreamError, LoggedErrorOf(QpackErrorCode.DecoderStreamError, () => new QpackEncoder(100, 0).ReadDecoderStream(FromHex(bytes))));
    }

    [TestMethod]
    public void ReadDecoderStream_EndlessIntegerContinuation_FailsAsDecoderStreamErrorWithinTenBytes()
    {
        var encoder = new QpackEncoder(100, 0);
        Diagnostics.Arrange("encoder", "100, 0");
        encoder.ReadDecoderStream(FromHex("ff 80808080 80808080"));

        Assert.AreEqual(QpackErrorCode.DecoderStreamError, LoggedErrorOf(QpackErrorCode.DecoderStreamError, () => encoder.ReadDecoderStream(FromHex("80"))));
    }

    [TestMethod]
    public void ReadDecoderStream_InstructionSplitAcrossReads_TakesEffectOnceComplete()
    {
        var encoder = new QpackEncoder(220, 1, huffmanCodeLiterals: false, tryIndexEveryName: true);
        Diagnostics.Arrange("encoder", "220, 1, huffmanCodeLiterals: false, tryIndexEveryName: true");
        encoder.TrySetDynamicTableCapacity(220);
        encoder.EncodeFieldSection(200, Fields(("x", "1")));

        encoder.ReadDecoderStream(FromHex("ff"));
        Assert.AreEqual(0, encoder.KnownReceivedCount);
        encoder.ReadDecoderStream(FromHex("49"));

        Diagnostics.Act("known received count", encoder.KnownReceivedCount);
        Diagnostics.Assert("known received count", 1L, encoder.KnownReceivedCount);
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
        Diagnostics.Arrange("encoder", "capacity, 16, huffmanCodeLiterals: false");
        encoder.TrySetDynamicTableCapacity(capacity);
        var decoder = new QpackDecoder(capacity, 16);
        decoder.ReadEncoderStream(encoder.TakeEncoderStreamBytes());

        var section = encoder.EncodeFieldSection(0, Fields((name, value)));

        Diagnostics.Act("section length", section.Length);
        Diagnostics.Bytes("section", section);
        Diagnostics.Diff("section", FromHex(expectedSection), section);
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
        Diagnostics.Arrange("encoder", "4096, 16, huffmanCodeLiterals: false");
        encoder.TrySetDynamicTableCapacity(4096);
        encoder.TryInsert(new HeaderField(name, value));
        encoder.TakeEncoderStreamBytes();

        var section = encoder.EncodeFieldSection(0, Fields((name, value)));

        Diagnostics.Act("section length", section.Length);
        Diagnostics.Bytes("section", section);
        Diagnostics.Diff("section", FromHex(expectedSection), section);
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
        Diagnostics.Arrange("encoder", "4096, 16, huffmanCodeLiterals: false");
        encoder.TrySetDynamicTableCapacity(4096);
        encoder.TakeEncoderStreamBytes();

        var section = encoder.EncodeFieldSection(0, Fields((name, value)));

        Diagnostics.Act("section length", section.Length);
        Diagnostics.Bytes("section", section);
        Diagnostics.Diff("section", FromHex(expectedSection), section);
        CollectionAssert.AreEqual(FromHex(expectedSection), section);
        Assert.IsEmpty(encoder.TakeEncoderStreamBytes());
        Assert.AreEqual(0, encoder.InsertCount);
    }

    [TestMethod]
    public void EncodeFieldSection_FieldOverThreeQuartersOfTheCapacity_IsNeverInserted()
    {
        var encoder = new QpackEncoder(100, 16, huffmanCodeLiterals: false, tryIndexEveryName: true);
        Diagnostics.Arrange("encoder", "100, 16, huffmanCodeLiterals: false, tryIndexEveryName: true");
        encoder.TrySetDynamicTableCapacity(100);
        encoder.TakeEncoderStreamBytes();
        var value = new string('v', 34);

        var section = encoder.EncodeFieldSection(0, Fields(("user-agent", value)));

        Diagnostics.Act("section length", section.Length);
        Diagnostics.Bytes("section", section);
        Diagnostics.Diff("section", FromHex("0000 5f50 22").Concat(Enumerable.Repeat((byte)'v', 34)).ToArray(), section);
        CollectionAssert.AreEqual(FromHex("0000 5f50 22").Concat(Enumerable.Repeat((byte)'v', 34)).ToArray(), section);
        Assert.IsEmpty(encoder.TakeEncoderStreamBytes());
    }

    [TestMethod]
    [DataRow("user-agent", "curl/8.21.0", "ff20 0b 6375726c2f382e32312e30")]
    [DataRow("host", "a", "44 686f7374 01 61")]
    public void EncodeFieldSection_NameNghttp3Stores_IsInserted(string name, string value, string expectedEncoderStream)
    {
        var encoder = new QpackEncoder(4096, 16, huffmanCodeLiterals: false);
        Diagnostics.Arrange("encoder", "4096, 16, huffmanCodeLiterals: false");
        encoder.TrySetDynamicTableCapacity(4096);
        encoder.TakeEncoderStreamBytes();

        var section = encoder.EncodeFieldSection(0, Fields((name, value)));

        Diagnostics.Act("section length", section.Length);
        Diagnostics.Bytes("section", section);
        Diagnostics.Diff("section", FromHex("0280 10"), section);
        CollectionAssert.AreEqual(FromHex("0280 10"), section);
        CollectionAssert.AreEqual(FromHex(expectedEncoderStream), encoder.TakeEncoderStreamBytes());
    }

    [TestMethod]
    public void EncodeFieldSection_LiteralOnlyFieldAlreadyInTheDynamicTable_IsReferencedFromIt()
    {
        var encoder = new QpackEncoder(4096, 16, huffmanCodeLiterals: false);
        Diagnostics.Arrange("encoder", "4096, 16, huffmanCodeLiterals: false");
        encoder.TrySetDynamicTableCapacity(4096);
        encoder.TryInsert(new HeaderField(":path", "/x"));
        encoder.TakeEncoderStreamBytes();

        var section = encoder.EncodeFieldSection(0, Fields((":path", "/x")));

        Diagnostics.Act("section length", section.Length);
        Diagnostics.Bytes("section", section);
        Diagnostics.Diff("section", FromHex("0200 80"), section);
        CollectionAssert.AreEqual(FromHex("0200 80"), section);
        Assert.IsEmpty(encoder.TakeEncoderStreamBytes());
    }

    [TestMethod]
    public void EncodeFieldSection_CookieOfTwentyBytes_StaysIndexable()
    {
        const string Value = "a=0123456789abcdefgh";
        var withoutTable = new QpackEncoder(0, 0, huffmanCodeLiterals: false);
        Diagnostics.Arrange("encoder", "0, 0, huffmanCodeLiterals: false");
        var withTable = new QpackEncoder(4096, 16, huffmanCodeLiterals: false);
        withTable.TrySetDynamicTableCapacity(4096);
        withTable.TakeEncoderStreamBytes();

        CollectionAssert.AreEqual(
            FromHex("0000 55 14 613d3031323334353637383961626364656667 68"),
            withoutTable.EncodeFieldSection(0, Fields(("cookie", Value))));
        CollectionAssert.AreEqual(FromHex("0280 10"), withTable.EncodeFieldSection(0, Fields(("cookie", Value))));
        var encoderStream = withTable.TakeEncoderStreamBytes();
        Diagnostics.Bytes("encoder stream", encoderStream);
        Diagnostics.Act("insert count", withTable.InsertCount);
        Diagnostics.Diff("encoder stream", FromHex("c5 14 613d3031323334353637383961626364656667 68"), encoderStream);
        CollectionAssert.AreEqual(FromHex("c5 14 613d3031323334353637383961626364656667 68"), encoderStream);
    }

    private QpackErrorCode LoggedErrorOf(QpackErrorCode expected, Action action)
    {
        var error = ErrorOf(action);
        Diagnostics.Act("error", error);
        Diagnostics.Assert("error", expected, error);
        return error;
    }
}
