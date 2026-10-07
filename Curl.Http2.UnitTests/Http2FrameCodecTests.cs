using Curl.Testing;
using static Curl.Http2.Hpack;

namespace Curl.Http2;

/// <summary>
/// Checks <see cref="Http2FrameCodec" /> lays out the frame header of RFC 9113 section 4.1,
/// reads frames split across reads, and fails on truncated or oversized frames.
/// </summary>
[TestClass]
public sealed class Http2FrameCodecTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Serialize_Frame_WritesLengthTypeFlagsAndStreamBeforeThePayload()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", "Headers, flags 0x25, stream 0x01020304, payload aabbcc");

        var bytes = Http2FrameCodec.Serialize(new Http2Frame(Http2FrameType.Headers, 0x25, 0x01020304, new byte[] { 0xaa, 0xbb, 0xcc }));
        diagnostics.Bytes("serialized frame", bytes);
        diagnostics.Act("serialized frame length", bytes.Length);

        diagnostics.Diff("serialized frame", FromHex("000003 01 25 01020304 aabbcc"), bytes);
        CollectionAssert.AreEqual(FromHex("000003 01 25 01020304 aabbcc"), bytes);
    }

    [TestMethod]
    public void Serialize_PayloadOverTheLargestFrameSize_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var frame = new Http2Frame(Http2FrameType.Data, 0, 1, new byte[Http2FrameCodec.LargestMaximumFrameSize + 1]);
        diagnostics.Arrange("payload length", frame.Payload.Length);
        diagnostics.Arrange("largest maximum frame size", Http2FrameCodec.LargestMaximumFrameSize);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http2FrameCodec.Serialize(frame));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task WriteAsync_Frame_WritesItsSerializedBytes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var output = new MemoryStream();
        var frame = Http2FrameFactory.CreatePing(0x0102030405060708, isAcknowledgement: true);
        diagnostics.Arrange("frame", $"{frame.Type}, flags {frame.Flags}, stream {frame.StreamId}, payload length {frame.Payload.Length}");

        await Http2FrameCodec.WriteAsync(output, frame, CancellationToken.None);
        diagnostics.Bytes("written bytes", output.ToArray());
        diagnostics.Act("written bytes length", output.ToArray().Length);

        diagnostics.Diff("written bytes", FromHex("000008 06 01 00000000 0102030405060708"), output.ToArray());
        CollectionAssert.AreEqual(FromHex("000008 06 01 00000000 0102030405060708"), output.ToArray());
    }

    [TestMethod]
    public async Task ReadAsync_FrameArrivingOneByteAtATime_ReadsTheWholeFrame()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var wire = FromHex("000003 00 01 00000005 616263 000000 04 01 00000000");
        using var input = new PeerStream(wire, readSize: 1);
        diagnostics.Bytes("wire bytes", wire);
        diagnostics.Arrange("wire bytes length", wire.Length);

        var first = await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None);
        var second = await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None);
        diagnostics.Act("first frame", $"{first!.Type}, flags {first.Flags}, stream {first.StreamId}, payload length {first.Payload.Length}");
        diagnostics.Act("second frame", $"{second!.Type}, flags {second.Flags}, stream {second.StreamId}, payload length {second.Payload.Length}");

        diagnostics.Assert("first frame type", Http2FrameType.Data, first.Type);
        diagnostics.Assert("second frame type", Http2FrameType.Settings, second.Type);
        Http2Test.AssertFrame(new Http2Frame(Http2FrameType.Data, Http2FrameFlags.EndStream, 5, "abc"u8.ToArray()), first!);
        Http2Test.AssertFrame(Http2FrameFactory.CreateSettingsAcknowledgement(), second!);
    }

    [TestMethod]
    public async Task ReadAsync_ReservedBitSet_IgnoresIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var wire = FromHex("000000 08 00 80000003");
        using var input = new MemoryStream(wire);
        diagnostics.Bytes("wire bytes", wire);
        diagnostics.Arrange("wire bytes length", wire.Length);

        var frame = await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None);
        diagnostics.Act("stream id", frame!.StreamId);

        diagnostics.Assert("stream id", 3, frame.StreamId);
        Assert.AreEqual(3, frame!.StreamId);
    }

    [TestMethod]
    public async Task ReadAsync_UnknownType_KeepsTheRawValue()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var wire = FromHex("000001 fa 07 00000000 ff");
        using var input = new MemoryStream(wire);
        diagnostics.Bytes("wire bytes", wire);
        diagnostics.Arrange("wire bytes length", wire.Length);

        var frame = await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None);
        diagnostics.Act("frame", $"type {(int)frame!.Type}, flags {frame.Flags}, stream {frame.StreamId}, payload length {frame.Payload.Length}");
        diagnostics.Bytes("payload", frame.Payload.Span);

        diagnostics.Assert("frame type", 0xfa, (int)frame.Type);
        Http2Test.AssertFrame(new Http2Frame((Http2FrameType)0xfa, 0x07, 0, new byte[] { 0xff }), frame!);
    }

    [TestMethod]
    public async Task ReadAsync_StreamEndedBetweenFrames_ReturnsNull()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var input = new MemoryStream();
        diagnostics.Arrange("input length", input.Length);

        var frame = await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None);
        diagnostics.Act("frame", frame);

        diagnostics.Assert("frame", null, frame);
        Assert.IsNull(await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_StreamEndedInTheHeader_ThrowsEndOfStream()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var wire = FromHex("000003 00 01");
        using var input = new MemoryStream(wire);
        diagnostics.Bytes("wire bytes", wire);
        diagnostics.Arrange("wire bytes length", wire.Length);

        var exception = await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception type", nameof(EndOfStreamException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task ReadAsync_StreamEndedInThePayload_ThrowsEndOfStream()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var wire = FromHex("000003 00 01 00000001 6162");
        using var input = new MemoryStream(wire);
        diagnostics.Bytes("wire bytes", wire);
        diagnostics.Arrange("wire bytes length", wire.Length);

        var exception = await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception type", nameof(EndOfStreamException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task ReadAsync_FrameOverTheMaximumSize_IsAFrameSizeError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var wire = FromHex("004001 00 00 00000001");
        using var input = new MemoryStream(wire);
        diagnostics.Bytes("wire bytes", wire);
        diagnostics.Arrange("wire bytes length", wire.Length);

        var exception = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None));
        diagnostics.Act("error code", exception.ErrorCode);
        diagnostics.Act("message", exception.Message);

        diagnostics.Assert("error code", Http2ErrorCode.FrameSizeError, exception.ErrorCode);
        diagnostics.Assert("message", "HTTP/2 FrameSizeError: a frame of 16385 bytes exceeds the maximum frame size of 16384.", exception.Message);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, exception.ErrorCode);
        Assert.AreEqual("HTTP/2 FrameSizeError: a frame of 16385 bytes exceeds the maximum frame size of 16384.", exception.Message);
    }

    [TestMethod]
    public async Task ReadAsync_FrameOfExactlyTheMaximumSize_IsRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var input = new MemoryStream([.. FromHex("004000 00 00 00000001"), .. new byte[16384]]);
        diagnostics.Arrange("input length", input.Length);

        var frame = await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None);
        diagnostics.Act("payload length", frame!.Payload.Length);

        diagnostics.Assert("payload length", 16384, frame.Payload.Length);
        Assert.AreEqual(16384, frame!.Payload.Length);
    }

    [TestMethod]
    public void HasFlag_ChecksEveryBitOfTheFlag()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var frame = new Http2Frame(Http2FrameType.Headers, Http2FrameFlags.EndHeaders | Http2FrameFlags.Padded, 1, ReadOnlyMemory<byte>.Empty);
        diagnostics.Arrange("frame flags", frame.Flags);

        var hasEndHeaders = frame.HasFlag(Http2FrameFlags.EndHeaders);
        var hasPadded = frame.HasFlag(Http2FrameFlags.Padded);
        var hasEndStream = frame.HasFlag(Http2FrameFlags.EndStream);
        diagnostics.Act("has EndHeaders, Padded, EndStream", $"{hasEndHeaders}, {hasPadded}, {hasEndStream}");

        diagnostics.Assert("has EndHeaders, Padded, EndStream", "True, True, False", $"{hasEndHeaders}, {hasPadded}, {hasEndStream}");
        Assert.IsTrue(frame.HasFlag(Http2FrameFlags.EndHeaders));
        Assert.IsTrue(frame.HasFlag(Http2FrameFlags.Padded));
        Assert.IsFalse(frame.HasFlag(Http2FrameFlags.EndStream));
    }
}
