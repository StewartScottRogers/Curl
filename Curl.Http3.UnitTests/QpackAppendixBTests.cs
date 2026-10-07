using Curl.Http2;
using Curl.Testing;
using static Curl.Http3.Qpack;

namespace Curl.Http3;

/// <summary>
/// Replays RFC 9204 appendix B byte for byte: the encoder produces every published field
/// section and encoder stream instruction, and the decoder reads them back, producing
/// every published decoder stream instruction and table state. The encoder inserts every
/// name, as the appendix does, where curl's nghttp3 keeps :path literal. The examples run in order
/// on one connection whose decoder advertised a 220-byte table (MaxEntries 6).
/// </summary>
[TestClass]
public sealed class QpackAppendixBTests
{
    private const long MaximumTableCapacity = 220;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // Appendix B.1.
    private const string B1Section = "0000 510b 2f69 6e64 6578 2e68 746d 6c";

    // Appendix B.2.
    private const string B2EncoderStream =
        "3fbd01 c00f 7777 772e 6578 616d 706c 652e 636f 6d c10c 2f73 616d 706c 652f 7061 7468";

    private const string B2Section = "0381 10 11";
    private const string B2DecoderStream = "84";

    // Appendix B.3.
    private const string B3EncoderStream = "4a63 7573 746f 6d2d 6b65 790c 6375 7374 6f6d 2d76 616c 7565";
    private const string B3DecoderStream = "01";

    // Appendix B.4.
    private const string B4EncoderStream = "02";
    private const string B4Section = "0500 80 c1 81";
    private const string B4DecoderStream = "48";

    // Appendix B.5.
    private const string B5EncoderStream = "810d 6375 7374 6f6d 2d76 616c 7565 32";

    private static readonly HeaderField[] B2Fields = Fields((":authority", "www.example.com"), (":path", "/sample/path"));

    private static readonly HeaderField[] B4Fields =
        Fields((":authority", "www.example.com"), (":path", "/"), ("custom-key", "custom-value"));

    [TestMethod]
    public void Encoder_AppendixB1_EncodesAStaticNameReferenceLiteral()
    {
        var encoder = NewEncoder();
        Diagnostics.Arrange("encoder", "maximum table capacity 220, maximum blocked streams 100");

        var section = encoder.EncodeFieldSection(0, Fields((":path", "/index.html")));

        Diagnostics.Act("header list", ":path: /index.html");
        Diagnostics.Bytes("section", section);
        Diagnostics.Diff("section", FromHex(B1Section), section);
        CollectionAssert.AreEqual(FromHex(B1Section), section);
        Assert.IsEmpty(encoder.TakeEncoderStreamBytes());
    }

    [TestMethod]
    public void Encoder_AppendixB2_SetsCapacityInsertsBothFieldsAndReferencesThemPostBase()
    {
        var encoder = NewEncoder();
        Diagnostics.Arrange("encoder", "maximum table capacity 220, maximum blocked streams 100");

        Assert.IsTrue(encoder.TrySetDynamicTableCapacity(220));
        var section = encoder.EncodeFieldSection(4, B2Fields);

        Diagnostics.Act("header list", string.Join(", ", B2Fields));
        Diagnostics.Bytes("section", section);
        Diagnostics.Diff("section", FromHex(B2Section), section);
        Diagnostics.Assert("dynamic table size", 106L, encoder.DynamicTableSize);
        CollectionAssert.AreEqual(FromHex(B2EncoderStream), encoder.TakeEncoderStreamBytes());
        CollectionAssert.AreEqual(FromHex(B2Section), section);
        Assert.AreEqual(106, encoder.DynamicTableSize);
        encoder.ReadDecoderStream(FromHex(B2DecoderStream));
        Assert.AreEqual(2, encoder.KnownReceivedCount);
    }

    [TestMethod]
    public void Encoder_AppendixB3ToB5_ProducesTheSpeculativeInsertDuplicateSectionAndEvictingInsert()
    {
        var encoder = NewEncoder();
        Diagnostics.Arrange("encoder", "maximum table capacity 220, maximum blocked streams 100");
        encoder.TrySetDynamicTableCapacity(220);
        encoder.EncodeFieldSection(4, B2Fields);
        encoder.ReadDecoderStream(FromHex(B2DecoderStream));
        encoder.TakeEncoderStreamBytes();

        Diagnostics.Act("state after B.2", $"insert count {encoder.InsertCount}, table size {encoder.DynamicTableSize}");
        Assert.IsTrue(encoder.TryInsert(new HeaderField("custom-key", "custom-value")));
        CollectionAssert.AreEqual(FromHex(B3EncoderStream), encoder.TakeEncoderStreamBytes());
        Assert.AreEqual(160, encoder.DynamicTableSize);
        encoder.ReadDecoderStream(FromHex(B3DecoderStream));
        Assert.AreEqual(3, encoder.KnownReceivedCount);

        Assert.IsTrue(encoder.TryDuplicate(0));
        CollectionAssert.AreEqual(FromHex(B4EncoderStream), encoder.TakeEncoderStreamBytes());
        CollectionAssert.AreEqual(FromHex(B4Section), encoder.EncodeFieldSection(8, B4Fields));
        Assert.AreEqual(217, encoder.DynamicTableSize);
        encoder.ReadDecoderStream(FromHex(B4DecoderStream));

        Assert.IsTrue(encoder.TryInsert(new HeaderField("custom-key", "custom-value2")));
        CollectionAssert.AreEqual(FromHex(B5EncoderStream), encoder.TakeEncoderStreamBytes());
        Assert.AreEqual(215, encoder.DynamicTableSize);
        Diagnostics.Assert("state after B.5", "insert count 5, table size 215", $"insert count {encoder.InsertCount}, table size {encoder.DynamicTableSize}");
        Assert.AreEqual(5, encoder.InsertCount);
    }

    [TestMethod]
    public void Decoder_AppendixB1ToB5_DecodesEverySectionAndProducesEveryDecoderInstruction()
    {
        var decoder = NewDecoder();
        Diagnostics.Arrange("decoder", "maximum table capacity 220, maximum blocked streams 100");

        CollectionAssert.AreEqual(Fields((":path", "/index.html")), Decode(decoder, 0, FromHex(B1Section)));
        Assert.IsEmpty(decoder.TakeDecoderStreamBytes());

        decoder.ReadEncoderStream(FromHex(B2EncoderStream));
        Assert.AreEqual(106, decoder.DynamicTableSize);
        CollectionAssert.AreEqual(B2Fields, Decode(decoder, 4, FromHex(B2Section)));
        CollectionAssert.AreEqual(FromHex(B2DecoderStream), decoder.TakeDecoderStreamBytes());

        decoder.ReadEncoderStream(FromHex(B3EncoderStream));
        Assert.AreEqual(160, decoder.DynamicTableSize);
        CollectionAssert.AreEqual(FromHex(B3DecoderStream), decoder.TakeDecoderStreamBytes());

        Assert.IsFalse(decoder.TryDecodeFieldSection(8, FromHex(B4Section), out var blocked));
        Assert.IsNull(blocked);
        Assert.AreEqual(1, decoder.BlockedStreamCount);
        decoder.CancelStream(8);
        Assert.AreEqual(0, decoder.BlockedStreamCount);
        CollectionAssert.AreEqual(FromHex(B4DecoderStream), decoder.TakeDecoderStreamBytes());
        decoder.ReadEncoderStream(FromHex(B4EncoderStream));
        Assert.AreEqual(217, decoder.DynamicTableSize);

        decoder.ReadEncoderStream(FromHex(B5EncoderStream));
        Assert.AreEqual(215, decoder.DynamicTableSize);
        Assert.AreEqual(5, decoder.InsertCount);
        Diagnostics.Act("state after B.5", $"insert count {decoder.InsertCount}, table size {decoder.DynamicTableSize}, capacity {decoder.DynamicTableCapacity}");
        Diagnostics.Assert("state after B.5", "insert count 5, table size 215, capacity 220", $"insert count {decoder.InsertCount}, table size {decoder.DynamicTableSize}, capacity {decoder.DynamicTableCapacity}");
        Assert.AreEqual(220, decoder.DynamicTableCapacity);
    }

    [TestMethod]
    public void Decoder_AppendixB4SectionOnceTheDuplicateArrives_DecodesAndAcknowledges()
    {
        var decoder = NewDecoder();
        Diagnostics.Arrange("decoder", "maximum table capacity 220, maximum blocked streams 100");
        decoder.ReadEncoderStream(FromHex(B2EncoderStream + B3EncoderStream));
        Assert.IsFalse(decoder.TryDecodeFieldSection(8, FromHex(B4Section), out _));

        decoder.ReadEncoderStream(FromHex(B4EncoderStream));

        var decoded = Decode(decoder, 8, FromHex(B4Section));
        Diagnostics.Act("decoded", string.Join(", ", decoded));
        Diagnostics.Assert("decoded", string.Join(", ", B4Fields), string.Join(", ", decoded));
        CollectionAssert.AreEqual(B4Fields, decoded);
        Assert.AreEqual(0, decoder.BlockedStreamCount);
        CollectionAssert.AreEqual(FromHex("88"), decoder.TakeDecoderStreamBytes());
    }

    private static QpackEncoder NewEncoder() => new(MaximumTableCapacity, 100, huffmanCodeLiterals: false, tryIndexEveryName: true);

    private static QpackDecoder NewDecoder() => new(MaximumTableCapacity, 100);
}
