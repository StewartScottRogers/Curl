using Curl.Http2;
using Curl.Testing;
using static Curl.Http3.Qpack;

namespace Curl.Http3;

/// <summary>
/// Adversarial black-box tests of QPACK (BL-1500): <see cref="QpackDecoder" /> and
/// <see cref="QpackEncoder" /> fed integers past 2^62 - 1, string lengths past what any
/// table could hold, references to evicted and not yet inserted entries, more blocked
/// streams than advertised, instructions one byte at a time, interleaved streams, and
/// seeded random bytes. The oracle is RFC 9204; curl shows none of this on its command line.
/// </summary>
[TestClass]
public sealed class QpackAdversarialTests
{
    private const int FuzzSeed = 1500;

    // Set Dynamic Table Capacity=220, then Insert With Literal Name custom-key=custom-value (size 54).
    private const string CustomKeyTable = "3fbd01 4a637573746f6d2d6b6579 0c637573746f6d2d76616c7565";

    // Set Dynamic Table Capacity=70 (room for two 34-byte entries), then insert a=b, a=c and a=d,
    // so a=b is evicted.
    private const string ThreeInsertsIntoRoomForTwo = "3f27 416101 62 416101 63 416101 64";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void TryDecodeFieldSection_ReferenceToAnEvictedEntry_FailsAsDecompressionFailed()
    {
        Diagnostics.Arrange("section", "Required Insert Count 3, Base 3, indexed relative 2 (absolute 0, evicted)");
        var decoder = DecoderWith(70, 0, ThreeInsertsIntoRoomForTwo);

        var live = Decode(decoder, 0, FromHex("0400 80"));
        var error = ErrorOf(() => decoder.TryDecodeFieldSection(4, FromHex("0400 82"), out _));

        Diagnostics.Assert("live entry, then error", ("a: d", QpackErrorCode.DecompressionFailed), (string.Join(", ", live), error));
        CollectionAssert.AreEqual(new[] { new HeaderField("a", "d") }, live);
        Assert.AreEqual(QpackErrorCode.DecompressionFailed, error);
    }

    [TestMethod]
    [DataRow("02", DisplayName = "Duplicate of the evicted entry")]
    [DataRow("82 0165", DisplayName = "Insert naming the evicted entry")]
    [DataRow("03", DisplayName = "Duplicate past every entry ever inserted")]
    public void ReadEncoderStream_ReferenceToAnEvictedOrMissingEntry_FailsAsEncoderStreamError(string instruction)
    {
        Diagnostics.Arrange("instruction after three inserts into room for two", instruction);
        var decoder = DecoderWith(70, 0, ThreeInsertsIntoRoomForTwo);

        var error = ErrorOf(() => decoder.ReadEncoderStream(FromHex(instruction)));

        Diagnostics.Assert("error", QpackErrorCode.EncoderStreamError, error);
        Assert.AreEqual(QpackErrorCode.EncoderStreamError, error);
    }

    [TestMethod]
    [DataRow("0200 10", DisplayName = "post-base indexed at the Required Insert Count")]
    [DataRow("0200 00 0178", DisplayName = "post-base name reference at the Required Insert Count")]
    [DataRow("0200 81", DisplayName = "indexed below absolute 0")]
    [DataRow("0300 80", DisplayName = "Required Insert Count 2 promising an entry never sent, no stream may block")]
    [DataRow("0201 80", DisplayName = "Base past the Required Insert Count, reference never reaching it")]
    public void TryDecodeFieldSection_ReferenceToAnEntryNotYetInserted_FailsAsDecompressionFailed(string section)
    {
        Diagnostics.Arrange("section, one entry inserted, no blocked streams allowed", section);
        var decoder = DecoderWith(220, 0, CustomKeyTable);

        var error = ErrorOf(() => decoder.TryDecodeFieldSection(0, FromHex(section), out _));

        Diagnostics.Assert("error", QpackErrorCode.DecompressionFailed, error);
        Assert.AreEqual(QpackErrorCode.DecompressionFailed, error);
    }

    [TestMethod]
    public void TryDecodeFieldSection_BlockedStreamsPastTheLimitThenCancelledAndUnblocked_KeepsCountAndAcknowledges()
    {
        Diagnostics.Arrange("decoder", "capacity 220, one blocked stream allowed, empty table");
        QpackDecoder decoder = new(220, 1);
        var section = FromHex("0200 80");

        var firstBlock = decoder.TryDecodeFieldSection(0, section, out _);
        var sameStreamAgain = decoder.TryDecodeFieldSection(0, section, out _);
        var overLimit = ErrorOf(() => decoder.TryDecodeFieldSection(4, section, out _));
        decoder.CancelStream(0);
        var afterCancel = decoder.TryDecodeFieldSection(4, section, out _);
        decoder.ReadEncoderStream(FromHex(CustomKeyTable));
        var unblocked = Decode(decoder, 4, section);

        Diagnostics.Act("blocks, error", $"{firstBlock}, {sameStreamAgain}, {overLimit}, {afterCancel}");
        Assert.IsFalse(firstBlock);
        Assert.IsFalse(sameStreamAgain);
        Assert.AreEqual(QpackErrorCode.DecompressionFailed, overLimit);
        Assert.IsFalse(afterCancel);
        CollectionAssert.AreEqual(new[] { new HeaderField("custom-key", "custom-value") }, unblocked);
        Assert.AreEqual(0, decoder.BlockedStreamCount);
        var decoderStream = Convert.ToHexString(decoder.TakeDecoderStreamBytes());
        Diagnostics.Assert("decoder stream", "40 (cancel stream 0), 84 (acknowledge stream 4)", decoderStream);
        Assert.AreEqual("4084", decoderStream);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty section")]
    [DataRow("00", DisplayName = "prefix with no Base")]
    [DataRow("01 00", DisplayName = "Required Insert Count with no dynamic table")]
    [DataRow("00 80", DisplayName = "negative Base below zero")]
    [DataRow("ff ffffffffffffffffff 00 00", DisplayName = "Encoded Insert Count longer than 62 bits")]
    [DataRow("0000 51 7f ffffffff0f", DisplayName = "value length past 2^31 with no bytes behind it")]
    [DataRow("0000 51 05 6162", DisplayName = "value length past the section's end")]
    [DataRow("0000 ff24", DisplayName = "static index 99, one past the table")]
    [DataRow("0000 2f ffffffffffffffffff", DisplayName = "literal name length longer than 62 bits")]
    [DataRow("0000 29 ff 00", DisplayName = "Huffman-coded name of eight padding bits")]
    public void TryDecodeFieldSection_MalformedSection_FailsAsDecompressionFailed(string section)
    {
        Diagnostics.Arrange("section, no dynamic table", section);

        var error = ErrorOf(() => new QpackDecoder(0, 0).TryDecodeFieldSection(0, FromHex(section), out _));

        Diagnostics.Assert("error", QpackErrorCode.DecompressionFailed, error);
        Assert.AreEqual(QpackErrorCode.DecompressionFailed, error);
    }

    [TestMethod]
    public void TryDecodeFieldSection_LastStaticIndexAndEmptyStrings_Decode()
    {
        Diagnostics.Arrange("section", "static 98; literal name '' value ''; static name 0 with value ''");

        var fields = Decode(new QpackDecoder(0, 0), 0, FromHex("0000 ff23 20 00 50 00"));

        Diagnostics.Assert("fields", "x-frame-options: sameorigin, : , :authority: ", string.Join(", ", fields));
        CollectionAssert.AreEqual(
            new[] { new HeaderField("x-frame-options", "sameorigin"), new HeaderField(string.Empty, string.Empty), new HeaderField(":authority", string.Empty) },
            fields);
    }

    [TestMethod]
    [DataRow("3fbe01", DisplayName = "capacity 221, one past the advertised 220")]
    [DataRow("3fe1ffffffffffffff7f", DisplayName = "capacity longer than 62 bits")]
    [DataRow("3f02 416101 62", DisplayName = "34-byte entry into capacity 33")]
    [DataRow("3fbd01 5fe1ffff0f", DisplayName = "literal name length past the capacity, no bytes behind it")]
    [DataRow("3fbd01 c0 7fe1ffff0f", DisplayName = "static-name value length past the capacity, no bytes behind it")]
    [DataRow("3fbd01 61ff 00", DisplayName = "Huffman-coded name of eight padding bits")]
    [DataRow("3fbd01 ff24 00", DisplayName = "static name index 99 after a valid capacity")]
    [DataRow("00", DisplayName = "Duplicate in an empty table")]
    public void ReadEncoderStream_InvalidInstruction_FailsAsEncoderStreamError(string instructions)
    {
        Diagnostics.Arrange("encoder stream, advertised capacity 220", instructions);
        QpackDecoder decoder = new(220, 0);

        var error = ErrorOf(() => decoder.ReadEncoderStream(FromHex(instructions)));

        Diagnostics.Assert("error", QpackErrorCode.EncoderStreamError, error);
        Assert.AreEqual(QpackErrorCode.EncoderStreamError, error);
    }

    [TestMethod]
    public void ReadEncoderStream_EntryExactlyTheCapacity_IsInsertedAndTheCapacityItsMaximum()
    {
        Diagnostics.Arrange("encoder stream", "capacity 34, insert a=b (size 34); advertised maximum 34");
        QpackDecoder decoder = new(34, 0);

        decoder.ReadEncoderStream(FromHex("3f03 416101 62"));

        Diagnostics.Assert("InsertCount, DynamicTableSize", (1L, 34L), (decoder.InsertCount, decoder.DynamicTableSize));
        Assert.AreEqual(1L, decoder.InsertCount);
        Assert.AreEqual(34L, decoder.DynamicTableSize);
    }

    [TestMethod]
    public void ReadEncoderStream_IntegerThatNeverEnds_IsRefusedOnce62BitsArePassed()
    {
        Diagnostics.Arrange("encoder stream", "3f, then continuation bytes 80 one call at a time");
        QpackDecoder decoder = new(220, 0);
        decoder.ReadEncoderStream(FromHex("3f"));

        var calls = 0;
        var exception = Assert.ThrowsExactly<QpackException>(() =>
        {
            for (; calls < 64; calls++)
            {
                decoder.ReadEncoderStream(FromHex("80"));
            }
        });

        Diagnostics.Assert("refused after continuation bytes", "fewer than 64", calls + 1);
        Assert.AreEqual(QpackErrorCode.EncoderStreamError, exception.ErrorCode);
        Assert.IsLessThan(64, calls);
    }

    [TestMethod]
    public void ReadEncoderStream_OneBytePerCall_InsertsOnlyWhenTheLastByteArrives()
    {
        var bytes = FromHex(CustomKeyTable);
        Diagnostics.Bytes("encoder stream", bytes);
        QpackDecoder decoder = new(220, 0);

        for (var index = 0; index < bytes.Length; index++)
        {
            Assert.AreEqual(0L, decoder.InsertCount, $"before byte {index}");
            decoder.ReadEncoderStream(bytes.AsSpan(index, 1));
        }

        Diagnostics.Assert("InsertCount, DynamicTableSize", (1L, 54L), (decoder.InsertCount, decoder.DynamicTableSize));
        Assert.AreEqual(1L, decoder.InsertCount);
        Assert.AreEqual(54L, decoder.DynamicTableSize);
    }

    [TestMethod]
    [DataRow("00", DisplayName = "Insert Count Increment of zero")]
    [DataRow("01", DisplayName = "Insert Count Increment past the Insert Count")]
    [DataRow("84", DisplayName = "Section Acknowledgement with no section sent")]
    [DataRow("3fffffffffffffffffff", DisplayName = "Insert Count Increment longer than 62 bits")]
    public void ReadDecoderStream_InvalidInstruction_FailsAsDecoderStreamError(string instruction)
    {
        Diagnostics.Arrange("decoder stream into an encoder with an empty table", instruction);

        var error = ErrorOf(() => new QpackEncoder(220, 1).ReadDecoderStream(FromHex(instruction)));

        Diagnostics.Assert("error", QpackErrorCode.DecoderStreamError, error);
        Assert.AreEqual(QpackErrorCode.DecoderStreamError, error);
    }

    [TestMethod]
    public void ReadDecoderStream_SectionAcknowledgedTwiceAndUnknownStreamCancelled_RefusesOnlyTheSecondAcknowledgement()
    {
        Diagnostics.Arrange("encoder", "capacity 220, one blocked stream; a section on stream 4 that inserts");
        QpackEncoder encoder = new(220, 1);
        encoder.TrySetDynamicTableCapacity(220);
        encoder.EncodeFieldSection(4, Fields(("user-agent", "curl/8.21.0")));

        encoder.ReadDecoderStream(FromHex("01 48"));
        encoder.ReadDecoderStream(FromHex("84"));
        var error = ErrorOf(() => encoder.ReadDecoderStream(FromHex("84")));

        Diagnostics.Assert("KnownReceivedCount, error", (1L, QpackErrorCode.DecoderStreamError), (encoder.KnownReceivedCount, error));
        Assert.AreEqual(1L, encoder.KnownReceivedCount);
        Assert.AreEqual(QpackErrorCode.DecoderStreamError, error);
    }

    [TestMethod]
    public void RoundTrip_SectionsDecodedBeforeTheirEncoderStreamAndOutOfOrder_GiveTheirFields()
    {
        Diagnostics.Arrange("streams", "sections on 0, 4 and 8 sharing fields; decoded 8, 4, 0, the encoder stream arriving between");
        QpackEncoder encoder = new(220, 2);
        QpackDecoder decoder = new(220, 2);
        encoder.TrySetDynamicTableCapacity(220);
        var fields = Fields((":method", "GET"), ("user-agent", "curl/8.21.0"), ("accept", "*/*"));
        var sections = new[] { 0L, 4L, 8L }.Select(stream => (Stream: stream, Bytes: encoder.EncodeFieldSection(stream, fields))).ToArray();

        var blocked = Enumerable.Reverse(sections).Count(section => !decoder.TryDecodeFieldSection(section.Stream, section.Bytes, out _));
        decoder.ReadEncoderStream(encoder.TakeEncoderStreamBytes());
        var decoded = Enumerable.Reverse(sections).Select(section => Decode(decoder, section.Stream, section.Bytes)).ToArray();
        encoder.ReadDecoderStream(decoder.TakeDecoderStreamBytes());

        Diagnostics.Act("blocked on first try", blocked);
        Diagnostics.Assert("KnownReceivedCount", encoder.InsertCount, encoder.KnownReceivedCount);
        Assert.IsLessThanOrEqualTo(2, blocked);
        foreach (var section in decoded)
        {
            CollectionAssert.AreEqual(fields, section);
        }

        Assert.AreEqual(0, decoder.BlockedStreamCount);
        Assert.AreEqual(encoder.InsertCount, encoder.KnownReceivedCount);
    }

    [TestMethod]
    public void TryDecodeFieldSection_SeededRandomSections_DecodeBlockOrFailWithAQpackErrorOnly()
    {
        Diagnostics.Arrange("seed", FuzzSeed);
        Random random = new(FuzzSeed);
        var decoder = DecoderWith(220, 2, CustomKeyTable);

        for (var round = 0; round < 3000; round++)
        {
            var bytes = new byte[random.Next(0, 32)];
            random.NextBytes(bytes);
            AssertOnlyQpackErrors(round, bytes, () => decoder.TryDecodeFieldSection(round, bytes, out _));
        }

        Diagnostics.Assert("rounds", 3000, "all decoded, blocked or refused");
    }

    [TestMethod]
    public void ReadEncoderAndDecoderStreams_SeededRandomBytes_ApplyOrFailWithAQpackErrorOnly()
    {
        Diagnostics.Arrange("seed", FuzzSeed);
        Random random = new(FuzzSeed);

        for (var round = 0; round < 3000; round++)
        {
            var bytes = new byte[random.Next(1, 32)];
            random.NextBytes(bytes);
            AssertOnlyQpackErrors(round, bytes, () => DecoderWith(220, 2, CustomKeyTable).ReadEncoderStream(bytes));
            AssertOnlyQpackErrors(round, bytes, () => EncoderWithOneSection().ReadDecoderStream(bytes));
        }

        Diagnostics.Assert("rounds", 3000, "all applied or refused");
    }

    private static QpackDecoder DecoderWith(long capacity, long blockedStreams, string encoderStream)
    {
        QpackDecoder decoder = new(capacity, blockedStreams);
        decoder.ReadEncoderStream(FromHex(encoderStream));
        return decoder;
    }

    private static QpackEncoder EncoderWithOneSection()
    {
        QpackEncoder encoder = new(220, 2);
        encoder.TrySetDynamicTableCapacity(220);
        encoder.EncodeFieldSection(0, Fields(("user-agent", "curl/8.21.0")));
        return encoder;
    }

    private void AssertOnlyQpackErrors(int round, byte[] bytes, Action action)
    {
        try
        {
            action();
        }
        catch (QpackException)
        {
            // A refusal the decoder or encoder documents.
        }
        catch (Exception exception)
        {
            Diagnostics.Act($"round {round}", Convert.ToHexString(bytes));
            Assert.Fail($"seed {FuzzSeed}, round {round}, bytes {Convert.ToHexString(bytes)}: {exception.GetType().Name}: {exception.Message}");
        }
    }
}
