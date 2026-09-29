using Curl.Http2;
using static Curl.Http3.Http3;

namespace Curl.Http3;

/// <summary>
/// Pins <see cref="Http3PeerQpackStreams" />: the server's QPACK encoder and decoder streams
/// fed to this endpoint's decoder and encoder (RFC 9204 section 4.2), and either stream
/// closing (RFC 9114 section 6.2.1).
/// </summary>
[TestClass]
public sealed class Http3PeerQpackStreamsTests
{
    [TestMethod]
    public async Task ReadEncoderStreamAsync_AppliesTheInstructionsToTheDecoder()
    {
        // Set Dynamic Table Capacity=220 (RFC 9204 appendix B.2).
        QpackDecoder decoder = new(220, 0);

        var count = await Http3PeerQpackStreams.ReadEncoderStreamAsync(StreamOf("3fbd01"), decoder, new byte[16], CancellationToken.None);

        Assert.AreEqual(3, count);
        Assert.AreEqual(220, decoder.DynamicTableCapacity);
    }

    [TestMethod]
    public async Task ReadDecoderStreamAsync_AppliesTheInstructionsToTheEncoder()
    {
        QpackEncoder encoder = new(220, 0);
        Assert.IsTrue(encoder.TrySetDynamicTableCapacity(220));
        Assert.IsTrue(encoder.TryInsert(new HeaderField("custom-key", "custom-value")));

        // Insert Count Increment of 1 (RFC 9204 section 4.4.3).
        var count = await Http3PeerQpackStreams.ReadDecoderStreamAsync(StreamOf("01"), encoder, new byte[16], CancellationToken.None);

        Assert.AreEqual(1, count);
        Assert.AreEqual(1, encoder.KnownReceivedCount);
    }

    [TestMethod]
    public async Task ReadQpackStreamsAsync_StreamEnding_IsClosedCriticalStream()
    {
        Assert.AreEqual(
            Http3ErrorCode.ClosedCriticalStream,
            await ErrorOfAsync(async () => await Http3PeerQpackStreams.ReadEncoderStreamAsync(StreamOf(""), new QpackDecoder(0, 0), new byte[16], CancellationToken.None)));
        Assert.AreEqual(
            Http3ErrorCode.ClosedCriticalStream,
            await ErrorOfAsync(async () => await Http3PeerQpackStreams.ReadDecoderStreamAsync(StreamOf(""), new QpackEncoder(0, 0), new byte[16], CancellationToken.None)));
    }

    [TestMethod]
    public async Task ReadQpackStreamsAsync_InvalidArguments_AreRejected()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await Http3PeerQpackStreams.ReadEncoderStreamAsync(StreamOf("00"), null!, new byte[16], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await Http3PeerQpackStreams.ReadDecoderStreamAsync(StreamOf("00"), null!, new byte[16], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await Http3PeerQpackStreams.ReadEncoderStreamAsync(null!, new QpackDecoder(0, 0), new byte[16], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () =>
            await Http3PeerQpackStreams.ReadEncoderStreamAsync(StreamOf("00"), new QpackDecoder(0, 0), Memory<byte>.Empty, CancellationToken.None));
    }
}
