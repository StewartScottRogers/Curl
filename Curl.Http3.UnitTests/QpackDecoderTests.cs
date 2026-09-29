using Curl.Http2;
using static Curl.Http3.Qpack;

namespace Curl.Http3;

/// <summary>
/// Pins <see cref="QpackDecoder" />: every field line representation of RFC 9204 section
/// 4.5, every encoder instruction of section 4.3 split across reads, blocked streams
/// (section 2.1.2) and the connection errors of section 6.
/// </summary>
[TestClass]
public sealed class QpackDecoderTests
{
    // Set Dynamic Table Capacity=220, then Insert With Literal Name custom-key=custom-value.
    private const string CustomKeyTable = "3fbd01 4a637573746f6d2d6b6579 0c637573746f6d2d76616c7565";

    [TestMethod]
    public void Constructor_NegativeLimits_AreRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QpackDecoder(-1, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QpackDecoder(0, -1));
    }

    [TestMethod]
    [DataRow("0200 60 03616263", "custom-key", "abc", true)]
    [DataRow("0280 08 03616263", "custom-key", "abc", true)]
    [DataRow("0280 00 03616263", "custom-key", "abc", false)]
    [DataRow("0200 80", "custom-key", "custom-value", false)]
    [DataRow("0000 32 6162 0163", "ab", "c", true)]
    [DataRow("0000 22 6162 0163", "ab", "c", false)]
    [DataRow("0000 71 0178", ":path", "x", true)]
    public void TryDecodeFieldSection_EachRepresentation_GivesItsFieldLine(string section, string name, string value, bool isNeverIndexed)
    {
        var decoder = DecoderWithCustomKey();

        var fields = Decode(decoder, 4, FromHex(section));

        CollectionAssert.AreEqual(new[] { new HeaderField(name, value, isNeverIndexed) }, fields);
    }

    [TestMethod]
    public void TryDecodeFieldSection_HuffmanCodedValue_IsDecoded() =>
        CollectionAssert.AreEqual(
            Fields(("ab", "custom-key")),
            Decode(new QpackDecoder(0, 0), 0, FromHex("0000 22 6162 88 25a849e95ba97d7f")));

    [TestMethod]
    [DataRow("00")]
    [DataRow("0000 51 05 6162")]
    [DataRow("0000 ff24")]
    [DataRow("0000 51 84 ffffffff")]
    [DataRow("0200 10")]
    [DataRow("0300 c1")]
    [DataRow("0281")]
    [DataRow("0d00")]
    public void TryDecodeFieldSection_InvalidSection_FailsAsDecompressionFailed(string section)
    {
        var decoder = DecoderWithCustomKey();
        decoder.ReadEncoderStream(FromHex("4161 0131"));

        Assert.AreEqual(QpackErrorCode.DecompressionFailed, ErrorOf(() => decoder.TryDecodeFieldSection(4, FromHex(section), out _)));
    }

    [TestMethod]
    public void TryDecodeFieldSection_BaseBeyond62Bits_FailsAsDecompressionFailed()
    {
        var section = new List<byte> { 0x02 };
        QpackPrimitives.WriteInteger(section, QpackPrimitives.LargestInteger, 7, 0x00);
        var decoder = DecoderWithCustomKey();

        Assert.AreEqual(QpackErrorCode.DecompressionFailed, ErrorOf(() => decoder.TryDecodeFieldSection(4, section.ToArray(), out _)));
    }

    [TestMethod]
    public void TryDecodeFieldSection_ReferenceToAnEvictedEntry_FailsAsDecompressionFailed()
    {
        var decoder = new QpackDecoder(220, 0);
        decoder.ReadEncoderStream(FromHex("3f21 4161 0131 4162 0132"));

        Assert.AreEqual(34, decoder.DynamicTableSize);
        Assert.AreEqual(QpackErrorCode.DecompressionFailed, ErrorOf(() => decoder.TryDecodeFieldSection(4, FromHex("0300 81"), out _)));
    }

    [TestMethod]
    public void TryDecodeFieldSection_RequiredInsertCountBeyondTheTableWithNoBlockingAllowed_FailsAsDecompressionFailed()
    {
        var decoder = new QpackDecoder(220, 0);

        Assert.AreEqual(QpackErrorCode.DecompressionFailed, ErrorOf(() => decoder.TryDecodeFieldSection(4, FromHex("0381 10 11"), out _)));
    }

    [TestMethod]
    public void TryDecodeFieldSection_BlockedStreams_AreCountedOncePerStreamUpToTheLimit()
    {
        var decoder = new QpackDecoder(220, 1);

        Assert.IsFalse(decoder.TryDecodeFieldSection(4, FromHex("0381 10 11"), out _));
        Assert.IsFalse(decoder.TryDecodeFieldSection(4, FromHex("0381 10 11"), out _));

        Assert.AreEqual(1, decoder.BlockedStreamCount);
        Assert.AreEqual(QpackErrorCode.DecompressionFailed, ErrorOf(() => decoder.TryDecodeFieldSection(8, FromHex("0381 10 11"), out _)));
    }

    [TestMethod]
    public void CancelStream_WithNoDynamicTable_WritesNothing()
    {
        var decoder = new QpackDecoder(0, 0);

        decoder.CancelStream(4);

        Assert.IsEmpty(decoder.TakeDecoderStreamBytes());
    }

    [TestMethod]
    public void ReadEncoderStream_InstructionsSplitAtEveryByte_TakeEffectOnceComplete()
    {
        var decoder = new QpackDecoder(220, 0);
        var bytes = FromHex(CustomKeyTable + " 00 80 03616263");

        foreach (var octet in bytes)
        {
            decoder.ReadEncoderStream([octet]);
        }

        Assert.AreEqual(3, decoder.InsertCount);
        CollectionAssert.AreEqual(
            Fields(("custom-key", "custom-value"), ("custom-key", "custom-value"), ("custom-key", "abc")),
            Decode(decoder, 4, FromHex("0400 82 81 80")));
    }

    [TestMethod]
    [DataRow("3fbe01")]
    [DataRow("3fbd01 ff24 00")]
    [DataRow("3fbd01 80 00")]
    [DataRow("3fbd01 00")]
    [DataRow("4161 0131")]
    [DataRow("3fbd01 41 61 81 ff")]
    public void ReadEncoderStream_InvalidInstruction_FailsAsEncoderStreamError(string bytes)
    {
        var decoder = new QpackDecoder(220, 0);

        Assert.AreEqual(QpackErrorCode.EncoderStreamError, ErrorOf(() => decoder.ReadEncoderStream(FromHex(bytes))));
    }

    [TestMethod]
    public void TakeDecoderStreamBytes_AfterInsertions_IncrementsTheInsertCountOnce()
    {
        var decoder = DecoderWithCustomKey();
        decoder.ReadEncoderStream(FromHex("00"));

        CollectionAssert.AreEqual(FromHex("02"), decoder.TakeDecoderStreamBytes());
        Assert.IsEmpty(decoder.TakeDecoderStreamBytes());
    }

    [TestMethod]
    public void QpackException_CarriesTheErrorCodeInItsMessage()
    {
        var exception = new QpackException(QpackErrorCode.DecoderStreamError, "reason");

        Assert.AreEqual("QPACK DecoderStreamError: reason.", exception.Message);
    }

    private static QpackDecoder DecoderWithCustomKey()
    {
        var decoder = new QpackDecoder(220, 0);
        decoder.ReadEncoderStream(FromHex(CustomKeyTable));
        return decoder;
    }
}
