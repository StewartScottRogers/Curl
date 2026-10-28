using Curl.Testing;
using static Curl.Http2.Hpack;
using static Curl.Http2.Http2FrameFactory;
using static Curl.Http2.Http2Test;

namespace Curl.Http2;

/// <summary>
/// Pins CGR-142 on <see cref="Http2Connection" />: a PUSH_PROMISE on a connection with push
/// disabled is a PROTOCOL_ERROR whose GOAWAY carries nghttp2's debug data <c>PUSH_PROMISE: push
/// disabled</c>, and the exception names the frame so the caller can report it as curl 8.22.0
/// does (measured, BL-3912 Notes).
/// </summary>
[TestClass]
public sealed class Cgr142Tests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadStreamFrameAsync_PushPromiseWithPushDisabled_SendsGoAwaySayingPushDisabled()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var peer = new PeerStream(Wire(CreatePushPromise(1, 2, new byte[] { 0x82, 0x86, 0x84 }, isEndHeaders: true)));
        var connection = new Http2Connection(peer);
        _ = connection.OpenStream();
        diagnostics.Arrange("peer frames", "PUSH_PROMISE stream 1 promising stream 2, 3-byte header block");

        var error = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => connection.ReadStreamFrameAsync(CancellationToken.None));
        diagnostics.Bytes("written", peer.Written.ToArray());

        byte[] expected = [.. FromHex("000023 07 00 00000000 00000000 00000001"), .. "PUSH_PROMISE: push disabled"u8];
        diagnostics.Diff("GOAWAY bytes", expected, peer.Written.ToArray());
        CollectionAssert.AreEqual(expected, peer.Written.ToArray());
        diagnostics.Assert("invalid frame type", Http2FrameType.PushPromise, error.InvalidFrame?.Type);
        Assert.AreEqual(Http2FrameType.PushPromise, error.InvalidFrame?.Type);
        Assert.AreEqual(7, error.InvalidFrame?.Payload.Length);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error.ErrorCode);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_OtherConnectionError_NamesNoInvalidFrameAndSendsNoDebugData()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var peer = new PeerStream(Wire(CreateContinuation(1, new byte[] { 0x88 }, isEndHeaders: true)));
        var connection = new Http2Connection(peer);
        diagnostics.Arrange("peer frames", "CONTINUATION on stream 1 with no header block open");

        var error = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => connection.ReadStreamFrameAsync(CancellationToken.None));

        diagnostics.Assert("invalid frame", null, error.InvalidFrame);
        Assert.IsNull(error.InvalidFrame);
        Assert.IsTrue(error.GoAwayDebugData.IsEmpty);
        CollectionAssert.AreEqual(FromHex("000008 07 00 00000000 00000000 00000001"), peer.Written.ToArray());
    }
}
