using System.Net;
using Curl.Http2;
using Curl.Http3;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

[TestClass]
public sealed class Http3StreamConnectionTests
{
    [TestMethod]
    public async Task WriteAsync_EmptyBodyWrite_SendsNoDataFrame()
    {
        FakeMultiplexedStream request = new(0, []);
        IHttpStreamConnection stream = new Http3Session(new FakeMultiplexedConnection(request)).CreateStream("https", null, ignoresBody: false);
        await stream.WriteAsync("GET / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);
        long headLength = request.Written.Length;

        await stream.WriteAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);

        Assert.AreEqual(headLength, request.Written.Length);
        Assert.IsFalse(request.IsEndedByClient);
    }

    [TestMethod]
    public async Task EndRequestAsync_AfterTheLastKnownBodyByte_SendsNothingMore()
    {
        FakeMultiplexedStream request = new(0, []);
        IHttpStreamConnection stream = new Http3Session(new FakeMultiplexedConnection(request)).CreateStream("https", 2, ignoresBody: false);
        await stream.WriteAsync("POST / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);
        await stream.WriteAsync("ab"u8.ToArray(), CancellationToken.None);
        long sent = request.Written.Length;

        await stream.EndRequestAsync(CancellationToken.None);

        Assert.AreEqual(sent, request.Written.Length);
        Assert.AreEqual(1, request.EndCount);
    }

    [TestMethod]
    public async Task ReadAsync_StreamRefusedWithRequestRejected_FailsMarkedRefusedAndStopsTheSessionTakingNewStreams()
    {
        // cf-ngtcp2.c at curl-8_21_0 (ADR-0187): the refused line, CURLE_RECV_ERROR, the connection closed to new requests.
        FakeMultiplexedStream request = new(4, []) { EndException = new MultiplexedStreamResetException(0x10b, "refused") };
        Http3Session session = new(new FakeMultiplexedConnection(request));
        IHttpStreamConnection stream = session.CreateStream("https", 0, ignoresBody: false);
        await stream.WriteAsync("GET / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);
        Assert.IsTrue(session.AcceptsNewStreams);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(async () => await stream.ReadAsync(new byte[16], CancellationToken.None));

        Assert.AreEqual(CurlExitCode.RecvError, thrown.ExitCode);
        Assert.AreEqual("HTTP/3 stream 4 refused by server, try again on a new connection", thrown.Message);
        Assert.IsTrue(thrown.IsStreamRefused);
        Assert.IsFalse(session.AcceptsNewStreams);
    }

    [TestMethod]
    [DataRow(0x101L, "0x101 GENERAL_PROTOCOL_ERROR")]
    [DataRow(0x102L, "0x102 INTERNAL_ERROR")]
    [DataRow(0x10cL, "0x10c REQUEST_CANCELLED")]
    [DataRow(0x10dL, "0x10d REQUEST_INCOMPLETE")]
    [DataRow(0x110L, "0x110 VERSION_FALLBACK")]
    [DataRow(0x21L, "0x21 NO_ERROR", DisplayName = "the first reserved greasing code")]
    [DataRow(0x40L, "0x40 NO_ERROR", DisplayName = "the second reserved greasing code")]
    [DataRow(0x22L, "0x22 unknown")]
    [DataRow(0x5L, "0x5 unknown")]
    [DataRow(0x111L, "0x111 unknown")]
    public async Task ReadAsync_StreamResetWithAnotherCode_FailsWithExit95NamingTheCodeAsCurlDoes(long errorCode, string named)
    {
        // vquic_h3_err_str at curl-8_21_0 (ADR-0187).
        FakeMultiplexedStream request = new(0, []) { EndException = new MultiplexedStreamResetException(errorCode, "reset") };
        Http3Session session = new(new FakeMultiplexedConnection(request));
        IHttpStreamConnection stream = session.CreateStream("https", 0, ignoresBody: false);
        await stream.WriteAsync("GET / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(async () => await stream.ReadAsync(new byte[16], CancellationToken.None));

        Assert.AreEqual(CurlExitCode.Http3, thrown.ExitCode);
        Assert.AreEqual($"HTTP/3 stream 0 reset by server (error {named})", thrown.Message);
        Assert.IsFalse(thrown.IsStreamRefused);
        Assert.IsTrue(session.AcceptsNewStreams);
    }

    [TestMethod]
    public async Task ReadToEndAsync_ResetAfterTheHeadWhenNoBodyIsWanted_EndsTheStream()
    {
        byte[] head = new Http3HeadersFrame(new QpackEncoder(0, 0).EncodeFieldSection(0, [new(":status", "200")])).ToBytes();
        FakeMultiplexedStream request = new(0, head) { EndException = new MultiplexedStreamResetException(0x10c, "reset") };
        IHttpStreamConnection stream = new Http3Session(new FakeMultiplexedConnection(request)).CreateStream("https", 0, ignoresBody: true);
        await stream.WriteAsync("HEAD / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);

        await stream.ReadToEndAsync(CancellationToken.None);

        Assert.AreEqual(0, await stream.ReadAsync(new byte[16], CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_ResetBeforeTheHeadWhenNoBodyIsWanted_StillFails()
    {
        FakeMultiplexedStream request = new(0, []) { EndException = new MultiplexedStreamResetException(0x10c, "reset") };
        IHttpStreamConnection stream = new Http3Session(new FakeMultiplexedConnection(request)).CreateStream("https", 0, ignoresBody: true);
        await stream.WriteAsync("HEAD / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(async () => await stream.ReadAsync(new byte[16], CancellationToken.None));

        Assert.AreEqual(CurlExitCode.Http3, thrown.ExitCode);
    }

    [TestMethod]
    public async Task Connection_Stream_IsSecureWithTheSessionsRemoteEndpointAndLeavesDisposalToTheSession()
    {
        IPEndPoint remote = new(IPAddress.Loopback, 443);
        FakeMultiplexedStream request = new(0, []);
        IHttpStreamConnection stream = new Http3Session(new FakeMultiplexedConnection(request) { RemoteEndPoint = remote }).CreateStream("https", 0, ignoresBody: false);
        await stream.WriteAsync("GET / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);

        await stream.DisposeAsync();

        Assert.IsTrue(stream.IsSecure);
        Assert.AreEqual(remote, stream.RemoteEndPoint);
        Assert.IsTrue(stream.TrailerBytes.IsEmpty);
        Assert.IsFalse(request.IsDisposed);
    }
}
