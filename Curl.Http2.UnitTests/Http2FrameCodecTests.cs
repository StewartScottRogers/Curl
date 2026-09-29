using static Curl.Http2.Hpack;

namespace Curl.Http2;

/// <summary>
/// Checks <see cref="Http2FrameCodec" /> lays out the frame header of RFC 9113 section 4.1,
/// reads frames split across reads, and fails on truncated or oversized frames.
/// </summary>
[TestClass]
public sealed class Http2FrameCodecTests
{
    [TestMethod]
    public void Serialize_Frame_WritesLengthTypeFlagsAndStreamBeforeThePayload()
    {
        var bytes = Http2FrameCodec.Serialize(new Http2Frame(Http2FrameType.Headers, 0x25, 0x01020304, new byte[] { 0xaa, 0xbb, 0xcc }));

        CollectionAssert.AreEqual(FromHex("000003 01 25 01020304 aabbcc"), bytes);
    }

    [TestMethod]
    public void Serialize_PayloadOverTheLargestFrameSize_Throws()
    {
        var frame = new Http2Frame(Http2FrameType.Data, 0, 1, new byte[Http2FrameCodec.LargestMaximumFrameSize + 1]);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http2FrameCodec.Serialize(frame));
    }

    [TestMethod]
    public async Task WriteAsync_Frame_WritesItsSerializedBytes()
    {
        using var output = new MemoryStream();
        var frame = Http2FrameFactory.CreatePing(0x0102030405060708, isAcknowledgement: true);

        await Http2FrameCodec.WriteAsync(output, frame, CancellationToken.None);

        CollectionAssert.AreEqual(FromHex("000008 06 01 00000000 0102030405060708"), output.ToArray());
    }

    [TestMethod]
    public async Task ReadAsync_FrameArrivingOneByteAtATime_ReadsTheWholeFrame()
    {
        using var input = new PeerStream(FromHex("000003 00 01 00000005 616263 000000 04 01 00000000"), readSize: 1);

        var first = await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None);
        var second = await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None);

        Http2Test.AssertFrame(new Http2Frame(Http2FrameType.Data, Http2FrameFlags.EndStream, 5, "abc"u8.ToArray()), first!);
        Http2Test.AssertFrame(Http2FrameFactory.CreateSettingsAcknowledgement(), second!);
    }

    [TestMethod]
    public async Task ReadAsync_ReservedBitSet_IgnoresIt()
    {
        using var input = new MemoryStream(FromHex("000000 08 00 80000003"));

        var frame = await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None);

        Assert.AreEqual(3, frame!.StreamId);
    }

    [TestMethod]
    public async Task ReadAsync_UnknownType_KeepsTheRawValue()
    {
        using var input = new MemoryStream(FromHex("000001 fa 07 00000000 ff"));

        var frame = await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None);

        Http2Test.AssertFrame(new Http2Frame((Http2FrameType)0xfa, 0x07, 0, new byte[] { 0xff }), frame!);
    }

    [TestMethod]
    public async Task ReadAsync_StreamEndedBetweenFrames_ReturnsNull()
    {
        using var input = new MemoryStream();

        Assert.IsNull(await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_StreamEndedInTheHeader_ThrowsEndOfStream()
    {
        using var input = new MemoryStream(FromHex("000003 00 01"));

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_StreamEndedInThePayload_ThrowsEndOfStream()
    {
        using var input = new MemoryStream(FromHex("000003 00 01 00000001 6162"));

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_FrameOverTheMaximumSize_IsAFrameSizeError()
    {
        using var input = new MemoryStream(FromHex("004001 00 00 00000001"));

        var exception = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None));

        Assert.AreEqual(Http2ErrorCode.FrameSizeError, exception.ErrorCode);
        Assert.AreEqual("HTTP/2 FrameSizeError: a frame of 16385 bytes exceeds the maximum frame size of 16384.", exception.Message);
    }

    [TestMethod]
    public async Task ReadAsync_FrameOfExactlyTheMaximumSize_IsRead()
    {
        using var input = new MemoryStream([.. FromHex("004000 00 00 00000001"), .. new byte[16384]]);

        var frame = await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None);

        Assert.AreEqual(16384, frame!.Payload.Length);
    }

    [TestMethod]
    public void HasFlag_ChecksEveryBitOfTheFlag()
    {
        var frame = new Http2Frame(Http2FrameType.Headers, Http2FrameFlags.EndHeaders | Http2FrameFlags.Padded, 1, ReadOnlyMemory<byte>.Empty);

        Assert.IsTrue(frame.HasFlag(Http2FrameFlags.EndHeaders));
        Assert.IsTrue(frame.HasFlag(Http2FrameFlags.Padded));
        Assert.IsFalse(frame.HasFlag(Http2FrameFlags.EndStream));
    }
}
