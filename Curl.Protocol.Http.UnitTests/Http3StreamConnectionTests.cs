using System.Net;
using Curl.Http2;
using Curl.Http3;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

[TestClass]
public sealed class Http3StreamConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WriteAsync_EmptyBodyWrite_SendsNoDataFrame()
    {
        FakeMultiplexedStream request = new(0, []);
        IHttpStreamConnection stream = new Http3Session(new FakeMultiplexedConnection(request)).CreateStream("https", null, ignoresBody: false);
        byte[] requestHead = "GET / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray();
        Diagnostics.Arrange("body write", "an empty write after the request head, body length unknown");
        Diagnostics.Bytes("request head", requestHead);
        await stream.WriteAsync(requestHead, CancellationToken.None);
        long headLength = request.Written.Length;

        await stream.WriteAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);

        Diagnostics.Act("bytes sent after the head and after the empty write", string.Join(", ", headLength, request.Written.Length));
        Diagnostics.Act("ended by client", request.IsEndedByClient);
        Diagnostics.Assert("bytes sent", headLength, request.Written.Length);
        Assert.AreEqual(headLength, request.Written.Length);
        Assert.IsFalse(request.IsEndedByClient);
    }

    [TestMethod]
    public async Task EndRequestAsync_AfterTheLastKnownBodyByte_SendsNothingMore()
    {
        FakeMultiplexedStream request = new(0, []);
        IHttpStreamConnection stream = new Http3Session(new FakeMultiplexedConnection(request)).CreateStream("https", 2, ignoresBody: false);
        Diagnostics.Arrange("request", "POST with a body length of 2, body ab");
        await stream.WriteAsync("POST / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);
        await stream.WriteAsync("ab"u8.ToArray(), CancellationToken.None);
        long sent = request.Written.Length;

        await stream.EndRequestAsync(CancellationToken.None);

        Diagnostics.Act("bytes sent before and after ending", string.Join(", ", sent, request.Written.Length));
        Diagnostics.Act("end count", request.EndCount);
        Diagnostics.Assert("end count", 1, request.EndCount);
        Assert.AreEqual(sent, request.Written.Length);
        Assert.AreEqual(1, request.EndCount);
    }

    [TestMethod]
    public async Task ReadAsync_StreamRefusedWithRequestRejected_FailsMarkedRefusedAndStopsTheSessionTakingNewStreams()
    {
        // cf-ngtcp2.c at curl-8_21_0 (ADR-0187): the refused line, CURLE_RECV_ERROR, the connection closed to new requests.
        FakeMultiplexedStream request = new(4, []) { EndException = new MultiplexedStreamResetException(0x10b, "refused") };
        Diagnostics.Arrange("request stream", "stream 4, reset by the server with 0x10b REQUEST_REJECTED");
        Http3Session session = new(new FakeMultiplexedConnection(request));
        IHttpStreamConnection stream = session.CreateStream("https", 0, ignoresBody: false);
        await stream.WriteAsync("GET / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);
        Assert.IsTrue(session.AcceptsNewStreams);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(async () => await stream.ReadAsync(new byte[16], CancellationToken.None));

        Diagnostics.Act("read failure", thrown.Message);
        Diagnostics.Act("exit code, stream refused, session accepts new streams", string.Join(", ", thrown.ExitCode, thrown.IsStreamRefused, session.AcceptsNewStreams));
        Diagnostics.Assert("read failure", "HTTP/3 stream 4 refused by server, try again on a new connection", thrown.Message);
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
        Diagnostics.Arrange("reset error code", $"0x{errorCode:x}");
        FakeMultiplexedStream request = new(0, []) { EndException = new MultiplexedStreamResetException(errorCode, "reset") };
        Http3Session session = new(new FakeMultiplexedConnection(request));
        IHttpStreamConnection stream = session.CreateStream("https", 0, ignoresBody: false);
        await stream.WriteAsync("GET / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(async () => await stream.ReadAsync(new byte[16], CancellationToken.None));

        Diagnostics.Act("read failure", thrown.Message);
        Diagnostics.Act("exit code, stream refused, session accepts new streams", string.Join(", ", thrown.ExitCode, thrown.IsStreamRefused, session.AcceptsNewStreams));
        Diagnostics.Assert("read failure", $"HTTP/3 stream 0 reset by server (error {named})", thrown.Message);
        Assert.AreEqual(CurlExitCode.Http3, thrown.ExitCode);
        Assert.AreEqual($"HTTP/3 stream 0 reset by server (error {named})", thrown.Message);
        Assert.IsFalse(thrown.IsStreamRefused);
        Assert.IsTrue(session.AcceptsNewStreams);
    }

    [TestMethod]
    public async Task ReadToEndAsync_ResetAfterTheHeadWhenNoBodyIsWanted_EndsTheStream()
    {
        byte[] head = new Http3HeadersFrame(new QpackEncoder(0, 0).EncodeFieldSection(0, [new(":status", "200")])).ToBytes();
        Diagnostics.Arrange("server response", "HEADERS :status 200, then reset with 0x10c; no body wanted");
        Diagnostics.Bytes("server HEADERS frame", head);
        FakeMultiplexedStream request = new(0, head) { EndException = new MultiplexedStreamResetException(0x10c, "reset") };
        IHttpStreamConnection stream = new Http3Session(new FakeMultiplexedConnection(request)).CreateStream("https", 0, ignoresBody: true);
        await stream.WriteAsync("HEAD / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);

        await stream.ReadToEndAsync(CancellationToken.None);

        int read = await stream.ReadAsync(new byte[16], CancellationToken.None);
        Diagnostics.Act("bytes read after the end", read);
        Diagnostics.Assert("bytes read after the end", 0, read);
        Assert.AreEqual(0, read);
    }

    [TestMethod]
    public async Task ReadAsync_ResetBeforeTheHeadWhenNoBodyIsWanted_StillFails()
    {
        Diagnostics.Arrange("server response", "reset with 0x10c before any HEADERS; no body wanted");
        FakeMultiplexedStream request = new(0, []) { EndException = new MultiplexedStreamResetException(0x10c, "reset") };
        IHttpStreamConnection stream = new Http3Session(new FakeMultiplexedConnection(request)).CreateStream("https", 0, ignoresBody: true);
        await stream.WriteAsync("HEAD / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(async () => await stream.ReadAsync(new byte[16], CancellationToken.None));

        Diagnostics.Act("read failure", thrown.Message);
        Diagnostics.Assert("exit code", CurlExitCode.Http3, thrown.ExitCode);
        Assert.AreEqual(CurlExitCode.Http3, thrown.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataFrameOneByteOverTheFramePayloadLimit_DeliversTheWholeBodyWithExit0()
    {
        // nghttp3 streams DATA of any length (BL-838); only other frames are held to the limit.
        byte[] body = new byte[Http3StreamConnection.MaximumFramePayloadLength + 1];
        new Random(838).NextBytes(body);
        byte[] head = new Http3HeadersFrame(new QpackEncoder(0, 0).EncodeFieldSection(0, [new(":status", "200")])).ToBytes();
        Diagnostics.Arrange("DATA payload length", body.Length);
        Diagnostics.Bytes("server HEADERS frame", head);
        FakeMultiplexedStream request = new(0, [.. head, .. new Http3DataFrame(body).ToBytes()]);
        MemoryStream output = new();

        TransferResult result = await Http3Handler(request).ExecuteAsync(Http3Context(output));

        Diagnostics.Act("exit code and error message", string.Join(", ", result.ExitCode, result.ErrorMessage ?? "none"));
        Diagnostics.Act("bytes written to the output", output.Length);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsTrue(body.AsSpan().SequenceEqual(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadersFrameOverTheFramePayloadLimit_FailsWithExit56AndExcessiveLoad()
    {
        // A HEADERS frame's type and a four-byte length one over the limit; the payload need not follow.
        byte[] incoming = [0x01, 0x81, 0x00, 0x00, 0x01];
        Diagnostics.Arrange("server frame header", "HEADERS with a length one over the frame payload limit");
        Diagnostics.Bytes("server bytes", incoming);
        FakeMultiplexedStream request = new(0, incoming);

        TransferResult result = await Http3Handler(request).ExecuteAsync(Http3Context(new MemoryStream()));

        Diagnostics.Act("exit code and error message", string.Join(", ", result.ExitCode, result.ErrorMessage ?? "none"));
        Diagnostics.Assert("error message", "nghttp3_conn_read_stream returned error: ERR_H3_EXCESSIVE_LOAD", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("nghttp3_conn_read_stream returned error: ERR_H3_EXCESSIVE_LOAD", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ReadToEndAsync_BodyLongerThanTheDataBuffer_ReadsItAllAndEnds()
    {
        byte[] head = new Http3HeadersFrame(new QpackEncoder(0, 0).EncodeFieldSection(0, [new(":status", "200")])).ToBytes();
        Diagnostics.Arrange("DATA payload length", (Http3StreamConnection.DataBufferLength * 2) + 1);
        Diagnostics.Bytes("server HEADERS frame", head);
        FakeMultiplexedStream request = new(0, [.. head, .. new Http3DataFrame(new byte[(Http3StreamConnection.DataBufferLength * 2) + 1]).ToBytes()]);
        IHttpStreamConnection stream = new Http3Session(new FakeMultiplexedConnection(request)).CreateStream("https", 0, ignoresBody: false);
        await stream.WriteAsync("GET / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);

        await stream.ReadToEndAsync(CancellationToken.None);

        int read = await stream.ReadAsync(new byte[16], CancellationToken.None);
        Diagnostics.Act("bytes read after the end", read);
        Diagnostics.Assert("bytes read after the end", 0, read);
        Assert.AreEqual(0, read);
    }

    [TestMethod]
    public async Task Connection_Stream_IsSecureWithTheSessionsRemoteEndpointAndLeavesDisposalToTheSession()
    {
        IPEndPoint remote = new(IPAddress.Loopback, 443);
        Diagnostics.Arrange("remote endpoint", remote);
        FakeMultiplexedStream request = new(0, []);
        IHttpStreamConnection stream = new Http3Session(new FakeMultiplexedConnection(request) { RemoteEndPoint = remote }).CreateStream("https", 0, ignoresBody: false);
        await stream.WriteAsync("GET / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);

        await stream.DisposeAsync();

        Diagnostics.Act("secure, remote endpoint, trailer length, request stream disposed", string.Join(", ", stream.IsSecure, stream.RemoteEndPoint, stream.TrailerBytes.Length, request.IsDisposed));
        Diagnostics.Assert("remote endpoint", remote, stream.RemoteEndPoint);
        Assert.IsTrue(stream.IsSecure);
        Assert.AreEqual(remote, stream.RemoteEndPoint);
        Assert.IsTrue(stream.TrailerBytes.IsEmpty);
        Assert.IsFalse(request.IsDisposed);
    }

    private static HttpProtocolHandler Http3Handler(FakeMultiplexedStream request)
    {
        QueueConnector connector = new();
        connector.MultiplexedResults.Enqueue(MultiplexedConnectResult.Connected(new FakeMultiplexedConnection(request), null));
        return new HttpProtocolHandler(connector, new SilentAuthenticator());
    }

    private static TransferContext Http3Context(Stream output) => new()
    {
        Url = CurlUrl.Parse("https://example.com/"),
        Output = output,
        TimeProvider = TimeProvider.System,
        ConnectTimeout = TimeSpan.FromSeconds(1),
        Http = new HttpRequestOptions { Version = HttpVersionPreference.Http3Only },
    };
}
