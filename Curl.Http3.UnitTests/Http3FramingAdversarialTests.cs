using Curl.Testing;
using static Curl.Http3.Http3;

namespace Curl.Http3;

/// <summary>
/// Adversarial black-box tests of HTTP/3 framing (BL-1500): <see cref="Http3FrameReader" />,
/// <see cref="Http3ControlStreamReader" />, <see cref="Http3PeerUnidirectionalStreams" /> and
/// <see cref="Http3PeerQpackStreams" /> fed variable-length integers at 2^62 - 1 and cut short,
/// grease and reserved types, duplicate and forbidden settings, frames in the wrong place,
/// one byte per read, and seeded random bytes. The oracle is RFC 9114 and RFC 9000 section 16;
/// curl shows none of this on its command line.
/// </summary>
[TestClass]
public sealed class Http3FramingAdversarialTests
{
    private const int FuzzSeed = 1500;

    private const long LargestVariableLengthInteger = (1L << 62) - 1;

    // GOAWAY naming stream 2^62 - 4, the largest client-initiated bidirectional stream ID, in eight bytes.
    private const string LargestGoaway = "07 08 fffffffffffffffc";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ControlStream_GoawayNamingTheLargestClientBidirectionalStream_IsAccepted()
    {
        Diagnostics.Arrange("control stream", "SETTINGS (empty), " + LargestGoaway);
        Http3ControlStreamReader reader = new(StreamOf("04 00 " + LargestGoaway));

        await reader.ReadFrameAsync(CancellationToken.None);
        await reader.ReadFrameAsync(CancellationToken.None);

        Diagnostics.Assert("GoawayStreamId", (1L << 62) - 4, reader.GoawayStreamId);
        Assert.AreEqual((1L << 62) - 4, reader.GoawayStreamId);
    }

    [TestMethod]
    public async Task ReadFrameAsync_UnknownTypeAtTheLargestVariableLengthInteger_IsSkipped()
    {
        Diagnostics.Arrange("stream", "type 2^62 - 1, length 0; GOAWAY 0");

        var frames = await ReadAllFramesAsync(StreamOf("ffffffffffffffff 00 07 01 00"));

        Diagnostics.Assert("frames", "GOAWAY 0", string.Join(", ", frames.Select(frame => frame.Type)));
        Assert.HasCount(1, frames);
        Assert.AreEqual(0L, ((Http3GoawayFrame)frames[0]).Id);
    }

    [TestMethod]
    public async Task ReadFrameAsync_KnownTypeClaimingTheLargestLength_FailsAsExcessiveLoadBeforeReadingIt()
    {
        Diagnostics.Arrange("stream", "HEADERS with length 2^62 - 1 and no payload");
        Http3FrameReader reader = new(StreamOf("01 ffffffffffffffff"), 1024);

        var error = await ErrorOfAsync(async () => await reader.ReadFrameAsync(CancellationToken.None));

        Diagnostics.Assert("error", Http3ErrorCode.ExcessiveLoad, error);
        Assert.AreEqual(Http3ErrorCode.ExcessiveLoad, error);
    }

    [TestMethod]
    [DataRow("01 04 01020304", true)]
    [DataRow("01 05 0102030405", false)]
    public async Task ReadFrameAsync_PayloadAtAndOnePastTheLimit_IsReadThenRefused(string frame, bool isAccepted)
    {
        Diagnostics.Arrange("frame, limit 4", frame);
        Http3FrameReader reader = new(StreamOf(frame), 4);

        if (isAccepted)
        {
            var headers = (Http3HeadersFrame?)await reader.ReadFrameAsync(CancellationToken.None);
            Diagnostics.Assert("payload length", 4, headers?.EncodedFieldSection.Length);
            Assert.AreEqual(4, headers?.EncodedFieldSection.Length);
            return;
        }

        var error = await ErrorOfAsync(async () => await reader.ReadFrameAsync(CancellationToken.None));
        Diagnostics.Assert("error", Http3ErrorCode.ExcessiveLoad, error);
        Assert.AreEqual(Http3ErrorCode.ExcessiveLoad, error);
    }

    [TestMethod]
    public async Task ReadFrameAsync_UnknownTypeClaimingTheLargestLengthThenEnding_FailsAsFrameError()
    {
        Diagnostics.Arrange("stream", "type 0x0a, length 2^62 - 1, three payload bytes, end");
        Http3FrameReader reader = new(StreamOf("0a ffffffffffffffff 010203"), 1024);

        var error = await ErrorOfAsync(async () => await reader.ReadFrameAsync(CancellationToken.None));

        Diagnostics.Assert("error, HasReachedEndOfStream", (Http3ErrorCode.FrameError, true), (error, reader.HasReachedEndOfStream));
        Assert.AreEqual(Http3ErrorCode.FrameError, error);
        Assert.IsTrue(reader.HasReachedEndOfStream);
    }

    [TestMethod]
    public async Task ReadFrameAsync_StreamCutAtEveryOffsetOfAFrame_EndsCleanlyOnlyAtTheStart()
    {
        var whole = Qpack.FromHex(LargestGoaway);
        Diagnostics.Bytes("frame", whole);

        for (var cut = 0; cut < whole.Length; cut++)
        {
            Http3FrameReader reader = new(new MemoryStream(whole[..cut]), 1024);
            if (cut == 0)
            {
                Assert.IsNull(await reader.ReadFrameAsync(CancellationToken.None));
                continue;
            }

            var error = await ErrorOfAsync(async () => await reader.ReadFrameAsync(CancellationToken.None));
            Diagnostics.Act($"cut at {cut}", error);
            Assert.AreEqual(Http3ErrorCode.FrameError, error, $"cut at {cut}");
        }
    }

    [TestMethod]
    [DataRow("07 4001 00", 0L)]
    [DataRow("07 02 4000", 0L)]
    [DataRow("07 04 80000004", 4L)]
    [DataRow("07 08 c000000000000008", 8L)]
    public async Task ReadFrameAsync_IntegersInLongerEncodingsThanNeeded_AreAccepted(string frame, long expectedId)
    {
        Diagnostics.Arrange("frame", frame);

        var frames = await ReadAllFramesAsync(StreamOf(frame));

        Diagnostics.Assert("GOAWAY ID", expectedId, ((Http3GoawayFrame)frames.Single()).Id);
        Assert.AreEqual(expectedId, ((Http3GoawayFrame)frames.Single()).Id);
    }

    [TestMethod]
    [DataRow("07 00", DisplayName = "GOAWAY with no ID")]
    [DataRow("07 02 0000", DisplayName = "GOAWAY with a byte after its ID")]
    [DataRow("07 01 40", DisplayName = "GOAWAY whose ID runs past its payload")]
    [DataRow("03 00", DisplayName = "CANCEL_PUSH with no push ID")]
    [DataRow("03 02 0101", DisplayName = "CANCEL_PUSH with a byte after its push ID")]
    [DataRow("0d 00", DisplayName = "MAX_PUSH_ID with no push ID")]
    [DataRow("0d 03 010203", DisplayName = "MAX_PUSH_ID with bytes after its push ID")]
    [DataRow("05 00", DisplayName = "PUSH_PROMISE with no push ID")]
    [DataRow("05 01 80", DisplayName = "PUSH_PROMISE whose push ID runs past its payload")]
    [DataRow("04 01 06", DisplayName = "SETTINGS identifier with no value")]
    [DataRow("04 02 06 40", DisplayName = "SETTINGS value cut short")]
    public async Task ReadFrameAsync_PayloadNotLaidOutAsItsTypeRequires_FailsAsFrameError(string frame)
    {
        Diagnostics.Arrange("frame", frame);

        var error = await ErrorOfAsync(() => ReadAllFramesAsync(StreamOf(frame)));

        Diagnostics.Assert("error", Http3ErrorCode.FrameError, error);
        Assert.AreEqual(Http3ErrorCode.FrameError, error);
    }

    [TestMethod]
    [DataRow("04 04 0601 0602", DisplayName = "known identifier twice")]
    [DataRow("04 04 2100 2101", DisplayName = "grease identifier twice")]
    [DataRow("04 02 0200", DisplayName = "HTTP/2 ENABLE_PUSH")]
    [DataRow("04 02 0500", DisplayName = "HTTP/2 MAX_FRAME_SIZE")]
    [DataRow("04 02 0802", DisplayName = "ENABLE_CONNECT_PROTOCOL of 2")]
    [DataRow("04 09 33 ffffffffffffffff", DisplayName = "H3_DATAGRAM of 2^62 - 1")]
    public async Task ReadFrameAsync_ForbiddenSettings_FailAsSettingsError(string frame)
    {
        Diagnostics.Arrange("frame", frame);

        var error = await ErrorOfAsync(() => ReadAllFramesAsync(StreamOf(frame)));

        Diagnostics.Assert("error", Http3ErrorCode.SettingsError, error);
        Assert.AreEqual(Http3ErrorCode.SettingsError, error);
    }

    [TestMethod]
    public async Task ReadFrameAsync_SettingsWithTheLargestUnknownIdentifierAndValue_KeepsItAndDropsGrease()
    {
        Diagnostics.Arrange("frame", "SETTINGS 2^62 - 1 = 2^62 - 1, grease 0x40 = 7, H3_DATAGRAM = 1 in two bytes");

        var frames = await ReadAllFramesAsync(StreamOf("04 16 ffffffffffffffff ffffffffffffffff 4040 07 33 4001"));

        var settings = (Http3SettingsFrame)frames.Single();
        Diagnostics.Assert("settings", "2^62 - 1 = 2^62 - 1, 0x33 = 1", string.Join(", ", settings.Settings));
        Assert.HasCount(2, settings.Settings);
        Assert.AreEqual(LargestVariableLengthInteger, settings.GetValueOrDefault(LargestVariableLengthInteger, 0));
        Assert.AreEqual(1L, settings.GetValueOrDefault(Http3SettingIdentifier.H3Datagram, 0));
    }

    [TestMethod]
    [DataRow("02 00", DisplayName = "PRIORITY")]
    [DataRow("06 00", DisplayName = "PING")]
    [DataRow("08 00", DisplayName = "WINDOW_UPDATE")]
    [DataRow("09 00", DisplayName = "CONTINUATION")]
    [DataRow("4006 00", DisplayName = "PING in a two-byte type")]
    public async Task ReadFrameAsync_ReservedHttp2TypeOfEmptyPayload_FailsAsFrameUnexpected(string frame)
    {
        Diagnostics.Arrange("frame", frame);

        var error = await ErrorOfAsync(() => ReadAllFramesAsync(StreamOf(frame)));

        Diagnostics.Assert("error", Http3ErrorCode.FrameUnexpected, error);
        Assert.AreEqual(Http3ErrorCode.FrameUnexpected, error);
    }

    [TestMethod]
    [DataRow("00 00", DisplayName = "DATA")]
    [DataRow("01 00", DisplayName = "HEADERS")]
    [DataRow("05 01 00", DisplayName = "PUSH_PROMISE")]
    [DataRow("0d 01 00", DisplayName = "MAX_PUSH_ID")]
    [DataRow("04 00", DisplayName = "a second SETTINGS")]
    public async Task ControlStream_FrameNotAllowedAfterSettings_FailsAsFrameUnexpected(string frame)
    {
        Diagnostics.Arrange("control stream", "SETTINGS (empty), " + frame);

        var error = await ErrorOfControlStreamAsync("04 00 " + frame, 2);

        Diagnostics.Assert("error", Http3ErrorCode.FrameUnexpected, error);
        Assert.AreEqual(Http3ErrorCode.FrameUnexpected, error);
    }

    [TestMethod]
    [DataRow("07 01 00", Http3ErrorCode.MissingSettings, 1, DisplayName = "GOAWAY first")]
    [DataRow("21 00 07 01 00", Http3ErrorCode.MissingSettings, 1, DisplayName = "grease, then GOAWAY first")]
    [DataRow("04 00 07 01 01", Http3ErrorCode.IdError, 2, DisplayName = "GOAWAY naming client unidirectional stream 1")]
    [DataRow("04 00 07 01 02", Http3ErrorCode.IdError, 2, DisplayName = "GOAWAY naming server bidirectional stream 2")]
    [DataRow("04 00 07 01 03", Http3ErrorCode.IdError, 2, DisplayName = "GOAWAY naming server unidirectional stream 3")]
    [DataRow("04 00 07 01 04 07 01 08", Http3ErrorCode.IdError, 3, DisplayName = "GOAWAY raising its stream ID")]
    [DataRow("04 00 03 01 00", Http3ErrorCode.IdError, 2, DisplayName = "CANCEL_PUSH with no MAX_PUSH_ID sent")]
    [DataRow("04 00", Http3ErrorCode.ClosedCriticalStream, 2, DisplayName = "end after SETTINGS")]
    [DataRow("04 00 07", Http3ErrorCode.ClosedCriticalStream, 2, DisplayName = "end inside a frame")]
    public async Task ControlStream_FrameInTheWrongState_FailsWithItsConnectionError(string stream, Http3ErrorCode expected, int reads)
    {
        Diagnostics.Arrange("control stream", stream);

        var error = await ErrorOfControlStreamAsync(stream, reads);

        Diagnostics.Assert("error", expected, error);
        Assert.AreEqual(expected, error);
    }

    [TestMethod]
    public async Task ControlStream_RepeatedAndLoweredGoaway_KeepsTheLatest()
    {
        Diagnostics.Arrange("control stream", "SETTINGS, GOAWAY 8, GOAWAY 8, GOAWAY 4, GOAWAY 0");
        Http3ControlStreamReader reader = new(StreamOf("04 00 07 01 08 07 01 08 07 01 04 07 01 00"));

        List<long?> ids = [];
        for (var read = 0; read < 5; read++)
        {
            await reader.ReadFrameAsync(CancellationToken.None);
            ids.Add(reader.GoawayStreamId);
        }

        Diagnostics.Assert("GoawayStreamId after each read", "-, 8, 8, 4, 0", string.Join(", ", ids));
        CollectionAssert.AreEqual(new long?[] { null, 8, 8, 4, 0 }, ids);
    }

    [TestMethod]
    public async Task ControlStream_ReadAgainAfterItClosed_FailsAsClosedCriticalStreamEachTime()
    {
        Diagnostics.Arrange("control stream", "SETTINGS (empty), end");
        Http3ControlStreamReader reader = new(StreamOf("04 00"));
        await reader.ReadFrameAsync(CancellationToken.None);

        var first = await ErrorOfAsync(async () => await reader.ReadFrameAsync(CancellationToken.None));
        var second = await ErrorOfAsync(async () => await reader.ReadFrameAsync(CancellationToken.None));

        Diagnostics.Assert("errors", (Http3ErrorCode.ClosedCriticalStream, Http3ErrorCode.ClosedCriticalStream), (first, second));
        Assert.AreEqual(Http3ErrorCode.ClosedCriticalStream, first);
        Assert.AreEqual(Http3ErrorCode.ClosedCriticalStream, second);
    }

    [TestMethod]
    public async Task ReadFrameAsync_OneBytePerRead_GivesTheSameFramesAsWholeReads()
    {
        const string Frames = "21 03 aabbcc 04 07 01 4064 07 00 33 01 " + LargestGoaway + " 01 02 0000 0d 01 05";
        Diagnostics.Arrange("stream", Frames);

        var whole = await ReadAllFramesAsync(StreamOf(Frames));
        var trickled = await ReadAllFramesAsync(new OneBytePerReadStream(Qpack.FromHex(Frames)));

        var wholeBytes = string.Join(" ", whole.Select(frame => Convert.ToHexString(frame.ToBytes())));
        var trickledBytes = string.Join(" ", trickled.Select(frame => Convert.ToHexString(frame.ToBytes())));
        Diagnostics.Assert("frames", wholeBytes, trickledBytes);
        Assert.HasCount(4, whole);
        Assert.AreEqual(wholeBytes, trickledBytes);
    }

    [TestMethod]
    public async Task ReadFrameOrDataAsync_DataClaimingTheLargestLengthThenEnding_GivesItsBytesThenFrameError()
    {
        Diagnostics.Arrange("stream", "DATA with length 2^62 - 1, three payload bytes, end");
        Http3FrameReader reader = new(StreamOf("00 ffffffffffffffff 010203"), 16);
        var buffer = new byte[16];

        var first = await reader.ReadFrameOrDataAsync(buffer, CancellationToken.None);
        var error = await ErrorOfAsync(async () => await reader.ReadFrameOrDataAsync(buffer, CancellationToken.None));

        Diagnostics.Assert("first DataLength, then error", (3, Http3ErrorCode.FrameError), (first.DataLength, error));
        Assert.AreEqual(3, first.DataLength);
        Assert.AreEqual(Http3ErrorCode.FrameError, error);
    }

    [TestMethod]
    public async Task ReadFrameOrDataAsync_HeadersAfterTrailers_IsHandedOnForTheCallerToRefuse()
    {
        // The reader leaves which frame may follow which to its caller (its remarks), so a
        // HEADERS after the trailing HEADERS comes back as a frame like any other.
        Diagnostics.Arrange("stream", "HEADERS, DATA 'ab', HEADERS (trailers), HEADERS");
        Http3FrameReader reader = new(StreamOf("01 01 00 00 02 6162 01 01 00 01 01 00"), 16);
        var buffer = new byte[16];

        List<string> pieces = [];
        while (await reader.ReadFrameOrDataAsync(buffer, CancellationToken.None) is { IsEndOfStream: false } piece)
        {
            pieces.Add(piece.Frame?.Type.ToString() ?? $"{piece.DataLength} data bytes");
        }

        Diagnostics.Assert("pieces", "Headers, 2 data bytes, Headers, Headers", string.Join(", ", pieces));
        CollectionAssert.AreEqual(new[] { "Headers", "2 data bytes", "Headers", "Headers" }, pieces);
    }

    [TestMethod]
    public async Task ReadFrameAsync_CancelledBeforeTheCall_ThrowsOperationCanceled()
    {
        Diagnostics.Arrange("token", "cancelled before ReadFrameAsync");
        Http3FrameReader reader = new(StreamOf("07 01 00"), 16);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await reader.ReadFrameAsync(new CancellationToken(canceled: true)));

        Diagnostics.Assert("exception", "OperationCanceledException or derived", exception.GetType().Name);
    }

    [TestMethod]
    public async Task ReadFrameAsync_SeededRandomBytes_ReturnFramesOrFailWithAnHttp3ErrorOnly()
    {
        Diagnostics.Arrange("seed", FuzzSeed);
        Random random = new(FuzzSeed);

        for (var round = 0; round < 2000; round++)
        {
            var bytes = new byte[random.Next(0, 40)];
            random.NextBytes(bytes);
            try
            {
                await ReadAllFramesAsync(new MemoryStream(bytes));
            }
            catch (Http3Exception)
            {
                // A refusal the reader documents.
            }
            catch (Exception exception)
            {
                Diagnostics.Act($"round {round}", Convert.ToHexString(bytes));
                Assert.Fail($"seed {FuzzSeed}, round {round}, bytes {Convert.ToHexString(bytes)}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Diagnostics.Assert("rounds", 2000, "all returned or refused");
    }

    [TestMethod]
    [DataRow("ffffffffffffffff", DisplayName = "type 2^62 - 1")]
    [DataRow("21", DisplayName = "grease 0x21")]
    [DataRow("7fff", DisplayName = "unknown 0x3fff")]
    [DataRow("40", DisplayName = "type cut short")]
    [DataRow("", DisplayName = "no type at all")]
    public async Task AcceptUnidirectionalStream_UnknownGreaseOrCutType_IsIgnored(string stream)
    {
        Diagnostics.Arrange("stream", stream);

        var type = await new Http3PeerUnidirectionalStreams().AcceptAsync(StreamOf(stream), CancellationToken.None);

        Diagnostics.Assert("type", "null", type);
        Assert.IsNull(type);
    }

    [TestMethod]
    public async Task AcceptUnidirectionalStream_EachCriticalTypeInterleavedWithGreaseThenRepeated_FailsOnTheSecond()
    {
        Diagnostics.Arrange("streams", "control, grease, encoder, unknown, decoder in a two-byte type; then each again");
        Http3PeerUnidirectionalStreams streams = new();
        foreach (var type in new[] { "00", "21", "02", "7fff", "4003" })
        {
            await streams.AcceptAsync(StreamOf(type), CancellationToken.None);
        }

        foreach (var repeat in new[] { "00", "4002", "03" })
        {
            var error = await ErrorOfAsync(async () => await streams.AcceptAsync(StreamOf(repeat), CancellationToken.None));
            Diagnostics.Act($"repeat {repeat}", error);
            Assert.AreEqual(Http3ErrorCode.StreamCreationError, error, repeat);
        }
    }

    [TestMethod]
    [DataRow("01", DisplayName = "push stream")]
    [DataRow("4001", DisplayName = "push stream in a two-byte type")]
    public async Task AcceptUnidirectionalStream_PushStream_FailsAsIdError(string stream)
    {
        Diagnostics.Arrange("stream", stream);

        var error = await ErrorOfAsync(async () => await new Http3PeerUnidirectionalStreams().AcceptAsync(StreamOf(stream), CancellationToken.None));

        Diagnostics.Assert("error", Http3ErrorCode.IdError, error);
        Assert.AreEqual(Http3ErrorCode.IdError, error);
    }

    [TestMethod]
    public async Task PeerQpackStreams_EmptyBufferAndClosedStreams_AreRefused()
    {
        Diagnostics.Arrange("calls", "encoder stream with an empty buffer; encoder and decoder streams that ended");
        QpackDecoder decoder = new(0, 0);
        QpackEncoder encoder = new(0, 0);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            async () => await Http3PeerQpackStreams.ReadEncoderStreamAsync(StreamOf("00"), decoder, Memory<byte>.Empty, CancellationToken.None));
        var encoderStream = await ErrorOfAsync(
            async () => await Http3PeerQpackStreams.ReadEncoderStreamAsync(Stream.Null, decoder, new byte[8], CancellationToken.None));
        var decoderStream = await ErrorOfAsync(
            async () => await Http3PeerQpackStreams.ReadDecoderStreamAsync(Stream.Null, encoder, new byte[8], CancellationToken.None));

        Diagnostics.Assert("errors", (Http3ErrorCode.ClosedCriticalStream, Http3ErrorCode.ClosedCriticalStream), (encoderStream, decoderStream));
        Assert.AreEqual(Http3ErrorCode.ClosedCriticalStream, encoderStream);
        Assert.AreEqual(Http3ErrorCode.ClosedCriticalStream, decoderStream);
    }

    private static async Task<Http3ErrorCode> ErrorOfControlStreamAsync(string stream, int reads)
    {
        Http3ControlStreamReader reader = new(StreamOf(stream));
        return await ErrorOfAsync(async () =>
        {
            for (var read = 0; read < reads; read++)
            {
                await reader.ReadFrameAsync(CancellationToken.None);
            }
        });
    }

    /// <summary>
    /// A stream that hands over at most one byte per read, as a slow peer would.
    /// </summary>
    private sealed class OneBytePerReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(1, buffer.Length)]);

        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(1, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
}
