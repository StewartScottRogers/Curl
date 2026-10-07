using Curl.Http2;
using Curl.Testing;
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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Constructor_NegativeLimits_AreRejected()
    {
        Diagnostics.Arrange("limits", "capacity -1; blocked streams -1");

        var capacity = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QpackDecoder(-1, 0));
        var blocked = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QpackDecoder(0, -1));

        Diagnostics.Act("parameters", $"{capacity.ParamName}, {blocked.ParamName}");
        Diagnostics.Assert("exceptions", "ArgumentOutOfRangeException x2", "as expected");
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
        Diagnostics.Arrange("section", section);
        var decoder = DecoderWithCustomKey();
        Diagnostics.Arrange("decoder", "capacity 220, custom-key: custom-value inserted");

        var fields = Decode(decoder, 4, FromHex(section));

        Diagnostics.Act("fields", string.Join(", ", fields));
        Diagnostics.Assert("fields", new HeaderField(name, value, isNeverIndexed), string.Join(", ", fields));
        CollectionAssert.AreEqual(new[] { new HeaderField(name, value, isNeverIndexed) }, fields);
    }

    [TestMethod]
    public void TryDecodeFieldSection_HuffmanCodedValue_IsDecoded()
    {
        Diagnostics.Arrange("section", "0000 22 6162 88 25a849e95ba97d7f (ab: Huffman-coded custom-key)");

        var fields = Decode(new QpackDecoder(0, 0), 0, FromHex("0000 22 6162 88 25a849e95ba97d7f"));

        Diagnostics.Act("fields", string.Join(", ", fields));
        Diagnostics.Assert("fields", "ab: custom-key", string.Join(", ", fields));
        CollectionAssert.AreEqual(
            Fields(("ab", "custom-key")),
            fields);
    }

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
        Diagnostics.Arrange("section", section);
        var decoder = DecoderWithCustomKey();
        Diagnostics.Arrange("decoder", "capacity 220, custom-key: custom-value inserted");
        decoder.ReadEncoderStream(FromHex("4161 0131"));

        Assert.AreEqual(QpackErrorCode.DecompressionFailed, LoggedErrorOf(QpackErrorCode.DecompressionFailed, () => decoder.TryDecodeFieldSection(4, FromHex(section), out _)));
    }

    [TestMethod]
    public void TryDecodeFieldSection_BaseBeyond62Bits_FailsAsDecompressionFailed()
    {
        var section = new List<byte> { 0x02 };
        QpackPrimitives.WriteInteger(section, QpackPrimitives.LargestInteger, 7, 0x00);
        var decoder = DecoderWithCustomKey();
        Diagnostics.Arrange("decoder", "capacity 220, custom-key: custom-value inserted");

        Assert.AreEqual(QpackErrorCode.DecompressionFailed, LoggedErrorOf(QpackErrorCode.DecompressionFailed, () => decoder.TryDecodeFieldSection(4, section.ToArray(), out _)));
    }

    [TestMethod]
    public void TryDecodeFieldSection_ReferenceToAnEvictedEntry_FailsAsDecompressionFailed()
    {
        var decoder = new QpackDecoder(220, 0);
        Diagnostics.Arrange("decoder", "maximum capacity 220, maximum blocked streams 0");
        decoder.ReadEncoderStream(FromHex("3f21 4161 0131 4162 0132"));

        Assert.AreEqual(34, decoder.DynamicTableSize);
        Assert.AreEqual(QpackErrorCode.DecompressionFailed, LoggedErrorOf(QpackErrorCode.DecompressionFailed, () => decoder.TryDecodeFieldSection(4, FromHex("0300 81"), out _)));
    }

    [TestMethod]
    public void TryDecodeFieldSection_RequiredInsertCountBeyondTheTableWithNoBlockingAllowed_FailsAsDecompressionFailed()
    {
        var decoder = new QpackDecoder(220, 0);
        Diagnostics.Arrange("decoder", "maximum capacity 220, maximum blocked streams 0");

        Assert.AreEqual(QpackErrorCode.DecompressionFailed, LoggedErrorOf(QpackErrorCode.DecompressionFailed, () => decoder.TryDecodeFieldSection(4, FromHex("0381 10 11"), out _)));
    }

    [TestMethod]
    public void TryDecodeFieldSection_BlockedStreams_AreCountedOncePerStreamUpToTheLimit()
    {
        var decoder = new QpackDecoder(220, 1);
        Diagnostics.Arrange("decoder", "maximum capacity 220, maximum blocked streams 1");

        Assert.IsFalse(decoder.TryDecodeFieldSection(4, FromHex("0381 10 11"), out _));
        Assert.IsFalse(decoder.TryDecodeFieldSection(4, FromHex("0381 10 11"), out _));

        Assert.AreEqual(1, decoder.BlockedStreamCount);
        Assert.AreEqual(QpackErrorCode.DecompressionFailed, LoggedErrorOf(QpackErrorCode.DecompressionFailed, () => decoder.TryDecodeFieldSection(8, FromHex("0381 10 11"), out _)));
    }

    [TestMethod]
    public void CancelStream_WithNoDynamicTable_WritesNothing()
    {
        var decoder = new QpackDecoder(0, 0);
        Diagnostics.Arrange("decoder", "maximum capacity 0, maximum blocked streams 0");

        decoder.CancelStream(4);

        var cancelled = decoder.TakeDecoderStreamBytes();
        Diagnostics.Bytes("decoder stream", cancelled);
        Diagnostics.Act("decoder stream length", cancelled.Length);
        Diagnostics.Assert("decoder stream length", 0, cancelled.Length);

        Assert.IsEmpty(cancelled);
    }

    [TestMethod]
    public void ReadEncoderStream_InstructionsSplitAtEveryByte_TakeEffectOnceComplete()
    {
        var decoder = new QpackDecoder(220, 0);
        Diagnostics.Arrange("decoder", "maximum capacity 220, maximum blocked streams 0");
        var bytes = FromHex(CustomKeyTable + " 00 80 03616263");

        foreach (var octet in bytes)
        {
            decoder.ReadEncoderStream([octet]);
        }

        Diagnostics.Act("insert count", decoder.InsertCount);
        Diagnostics.Assert("insert count", 3L, decoder.InsertCount);
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
        Diagnostics.Arrange("bytes", bytes);
        var decoder = new QpackDecoder(220, 0);
        Diagnostics.Arrange("decoder", "maximum capacity 220, maximum blocked streams 0");

        Assert.AreEqual(QpackErrorCode.EncoderStreamError, LoggedErrorOf(QpackErrorCode.EncoderStreamError, () => decoder.ReadEncoderStream(FromHex(bytes))));
    }

    [TestMethod]
    [DataRow("3fbd01 5f8d02")]
    [DataRow("3fbd01 c0 7f34")]
    [DataRow("3fbd01 c0 ff9e04")]
    [DataRow("3fbd01 41 61 7f3d")]
    public void ReadEncoderStream_StringLongerThanTheTableAllows_FailsBeforeItsBytesArrive(string bytes)
    {
        Diagnostics.Arrange("bytes", bytes);
        // In a 220-byte table: 5f8d02 is a 300-byte literal name, above the advertised maximum;
        // beside :authority a value may have 178 characters (668 Huffman-coded bytes), and
        // beside the literal name "a", 187.
        var decoder = new QpackDecoder(220, 0);
        Diagnostics.Arrange("decoder", "maximum capacity 220, maximum blocked streams 0");

        Assert.AreEqual(QpackErrorCode.EncoderStreamError, LoggedErrorOf(QpackErrorCode.EncoderStreamError, () => decoder.ReadEncoderStream(FromHex(bytes))));
    }

    [TestMethod]
    [DataRow("3fbd01 c0 7f33")]
    [DataRow("3fbd01 c0 ff9d04")]
    [DataRow("3fbd01 41 61 7f3c")]
    public void ReadEncoderStream_StringThatCanFitTheTable_WaitsForItsBytes(string bytes)
    {
        Diagnostics.Arrange("bytes", bytes);
        var decoder = new QpackDecoder(220, 0);
        Diagnostics.Arrange("decoder", "maximum capacity 220, maximum blocked streams 0");

        decoder.ReadEncoderStream(FromHex(bytes));

        Diagnostics.Act("insert count", decoder.InsertCount);
        Diagnostics.Assert("insert count", 0L, decoder.InsertCount);
        Assert.AreEqual(0, decoder.InsertCount);
    }

    [TestMethod]
    public void ReadEncoderStream_LiteralNameWithNoCapacitySet_FailsBeforeItsBytesArrive()
    {
        Diagnostics.Arrange("encoder stream", "40 (literal name, no capacity set)");

        Assert.AreEqual(QpackErrorCode.EncoderStreamError, LoggedErrorOf(QpackErrorCode.EncoderStreamError, () => new QpackDecoder(220, 0).ReadEncoderStream(FromHex("40"))));
    }

    [TestMethod]
    public void TakeDecoderStreamBytes_AfterInsertions_IncrementsTheInsertCountOnce()
    {
        var decoder = DecoderWithCustomKey();
        Diagnostics.Arrange("decoder", "capacity 220, custom-key: custom-value inserted");
        decoder.ReadEncoderStream(FromHex("00"));

        var instructions = decoder.TakeDecoderStreamBytes();
        Diagnostics.Bytes("decoder stream", instructions);
        Diagnostics.Act("decoder stream length", instructions.Length);
        Diagnostics.Diff("decoder stream", FromHex("02"), instructions);
        CollectionAssert.AreEqual(FromHex("02"), instructions);
        Assert.IsEmpty(decoder.TakeDecoderStreamBytes());
    }

    [TestMethod]
    public void QpackException_CarriesTheErrorCodeInItsMessage()
    {
        Diagnostics.Arrange("error code, reason", "DecoderStreamError, reason");
        var exception = new QpackException(QpackErrorCode.DecoderStreamError, "reason");
        Diagnostics.Act("message", exception.Message);
        Diagnostics.Diff("message", "QPACK DecoderStreamError: reason.", exception.Message);

        Assert.AreEqual("QPACK DecoderStreamError: reason.", exception.Message);
    }

    private QpackErrorCode LoggedErrorOf(QpackErrorCode expected, Action action)
    {
        var error = ErrorOf(action);
        Diagnostics.Act("error", error);
        Diagnostics.Assert("error", expected, error);
        return error;
    }

    private static QpackDecoder DecoderWithCustomKey()
    {
        var decoder = new QpackDecoder(220, 0);
        decoder.ReadEncoderStream(FromHex(CustomKeyTable));
        return decoder;
    }
}
