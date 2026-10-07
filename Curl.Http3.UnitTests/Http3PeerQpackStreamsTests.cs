using Curl.Http2;
using Curl.Testing;
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
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadEncoderStreamAsync_AppliesTheInstructionsToTheDecoder()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoder stream", "3fbd01 (Set Dynamic Table Capacity 220)");

        // Set Dynamic Table Capacity=220 (RFC 9204 appendix B.2).
        QpackDecoder decoder = new(220, 0);

        var count = await Http3PeerQpackStreams.ReadEncoderStreamAsync(StreamOf("3fbd01"), decoder, new byte[16], CancellationToken.None);

        diagnostics.Act("bytes read", count);
        diagnostics.Act("decoder capacity", decoder.DynamicTableCapacity);
        diagnostics.Assert("bytes read", 3, count);
        Assert.AreEqual(3, count);
        diagnostics.Assert("decoder capacity", 220, decoder.DynamicTableCapacity);
        Assert.AreEqual(220, decoder.DynamicTableCapacity);
    }

    [TestMethod]
    public async Task ReadDecoderStreamAsync_AppliesTheInstructionsToTheEncoder()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoder table", "capacity 220, custom-key: custom-value inserted");
        diagnostics.Arrange("decoder stream", "01 (Insert Count Increment 1)");
        QpackEncoder encoder = new(220, 0);
        Assert.IsTrue(encoder.TrySetDynamicTableCapacity(220));
        Assert.IsTrue(encoder.TryInsert(new HeaderField("custom-key", "custom-value")));

        // Insert Count Increment of 1 (RFC 9204 section 4.4.3).
        var count = await Http3PeerQpackStreams.ReadDecoderStreamAsync(StreamOf("01"), encoder, new byte[16], CancellationToken.None);

        diagnostics.Act("bytes read", count);
        diagnostics.Act("known received count", encoder.KnownReceivedCount);
        diagnostics.Assert("bytes read", 1, count);
        Assert.AreEqual(1, count);
        diagnostics.Assert("known received count", 1L, encoder.KnownReceivedCount);
        Assert.AreEqual(1, encoder.KnownReceivedCount);
    }

    [TestMethod]
    public async Task ReadQpackStreamsAsync_StreamEnding_IsClosedCriticalStream()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("streams", "empty encoder stream, empty decoder stream");

        var encoderError = await ErrorOfAsync(async () => await Http3PeerQpackStreams.ReadEncoderStreamAsync(StreamOf(""), new QpackDecoder(0, 0), new byte[16], CancellationToken.None));
        var decoderError = await ErrorOfAsync(async () => await Http3PeerQpackStreams.ReadDecoderStreamAsync(StreamOf(""), new QpackEncoder(0, 0), new byte[16], CancellationToken.None));

        diagnostics.Act("encoder stream error", encoderError);
        diagnostics.Act("decoder stream error", decoderError);
        diagnostics.Assert("encoder stream error", Http3ErrorCode.ClosedCriticalStream, encoderError);
        Assert.AreEqual(Http3ErrorCode.ClosedCriticalStream, encoderError);
        diagnostics.Assert("decoder stream error", Http3ErrorCode.ClosedCriticalStream, decoderError);
        Assert.AreEqual(Http3ErrorCode.ClosedCriticalStream, decoderError);
    }

    [TestMethod]
    public async Task ReadQpackStreamsAsync_InvalidArguments_AreRejected()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("arguments", "null decoder; null encoder; null stream; empty buffer");

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await Http3PeerQpackStreams.ReadEncoderStreamAsync(StreamOf("00"), null!, new byte[16], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await Http3PeerQpackStreams.ReadDecoderStreamAsync(StreamOf("00"), null!, new byte[16], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await Http3PeerQpackStreams.ReadEncoderStreamAsync(null!, new QpackDecoder(0, 0), new byte[16], CancellationToken.None));
        var emptyBuffer = await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () =>
            await Http3PeerQpackStreams.ReadEncoderStreamAsync(StreamOf("00"), new QpackDecoder(0, 0), Memory<byte>.Empty, CancellationToken.None));

        diagnostics.Act("empty buffer's parameter", emptyBuffer.ParamName);
        diagnostics.Assert("exceptions", "ArgumentNullException x3, ArgumentOutOfRangeException", "as expected");
    }
}
