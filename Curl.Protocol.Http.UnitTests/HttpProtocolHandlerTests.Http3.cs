using System.Net;
using System.Text;
using Curl.Http2;
using Curl.Http3;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins the HTTP/3 exchange (BL-731, ADR-0172) through a fake QUIC connection: the request's
/// field section in the order curl.se's ngtcp2 build sends it (measured, ADR-0144), and the
/// output and exit codes for the responses and failures <c>curl_ngtcp2.c</c> reports.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    /// <summary>The client's control stream: its type, then curl's SETTINGS (ADR-0165).</summary>
    private static readonly byte[] CurlControlStream = [0x00, .. new Http3SettingsFrame(Http3LocalUnidirectionalStreams.CurlSettings).ToBytes()];

    [TestMethod]
    public async Task ExecuteAsync_Http3Get_SendsCurlsFieldSectionAndWritesTheHeadAndBody()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200", ("content-type", "text/plain"), ("content-length", "5")), Http3Data("hello")), chunkSize);
            FakeMultiplexedConnection quic = new(stream) { RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 443), LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000) };
            MemoryStream output = new();
            MemoryStream headerOutput = new();
            RecordingTransferEvents events = new();

            TransferResult result = await Handler(QuicConnector(quic))
                .ExecuteAsync(Http3Context("https://example.com/", output, headerOutput, events: events));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("HTTP/3 200 \r\ncontent-type: text/plain\r\ncontent-length: 5\r\n\r\n", Latin1(headerOutput.ToArray()));
            Assert.AreEqual("hello", Latin1(output.ToArray()));
            Assert.AreEqual(new Version(3, 0), result.Report!.HttpVersion);
            Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 443), result.Report.RemoteEndPoint);
            Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 50000), result.Report.LocalEndPoint);
            CollectionAssert.AreEqual(
                new[] { ":method: GET", ":scheme: https", ":authority: example.com", ":path: /", "user-agent: curl/8.18.0", "accept: */*" },
                await RequestFieldsAsync(stream));
            Assert.AreEqual(1, stream.EndCount, "a request without a body ends with its HEADERS");
            CollectionAssert.Contains(events.Info, "using HTTP/3");
            CollectionAssert.AreEqual(CurlControlStream, quic.UnidirectionalStreams[0].Written.ToArray());
            CollectionAssert.AreEqual(new byte[] { 0x02 }, quic.UnidirectionalStreams[1].Written.ToArray());
            CollectionAssert.AreEqual(new byte[] { 0x03 }, quic.UnidirectionalStreams[2].Written.ToArray());
            Assert.AreEqual(0x100L, quic.CloseCode, "closed with H3_NO_ERROR");
            Assert.IsTrue(quic.IsDisposed);
            Assert.IsTrue(stream.IsDisposed);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3GetHeaders_PinsTheEncodedFieldSection()
    {
        // A HEADERS frame of 27 bytes: the prefix (no dynamic table), then :method GET (static
        // 17), :scheme https (23), :authority (0) with Huffman "example.com", :path / (1),
        // user-agent (95) with Huffman "curl/8.18.0", accept: */* (29) - RFC 9204 section 4.5,
        // with Huffman coding where it is shorter, as nghttp3 1.15 encodes under curl's 0 capacity.
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("204")));

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream)))
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(
            "011b" + "0000" + "d1" + "d7" + "50882f91d35d055c87a7" + "c1" + "5f508825b650c3cb85e5c1" + "dd",
            Convert.ToHexString(stream.Written.ToArray()).ToLowerInvariant());
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3PostWithDataAndCustomHeaders_SendsThemInCurlsOrderThenOneDataFrameEndingTheStream()
    {
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200", ("content-length", "2")), Http3Data("ok")));
        MemoryStream output = new();

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream))).ExecuteAsync(Http3Context(
            "https://example.com/form",
            output,
            options: new HttpRequestOptions
            {
                Body = new BytesBody("name=value"u8.ToArray(), "application/x-www-form-urlencoded"),
                Headers = ["X-Custom: One", "Accept: text/html", "Connection: keep-alive"],
            }));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
        Assert.AreEqual(10L, result.Report!.UploadSize);
        CollectionAssert.AreEqual(
            new[]
            {
                ":method: POST", ":scheme: https", ":authority: example.com", ":path: /form", "user-agent: curl/8.18.0",
                "x-custom: One", "accept: text/html", "content-length: 10", "content-type: application/x-www-form-urlencoded",
            },
            await RequestFieldsAsync(stream));
        List<Http3Frame> frames = await RequestFramesAsync(stream);
        Assert.AreEqual(2, frames.Count);
        Assert.AreEqual("name=value", Latin1(((Http3DataFrame)frames[1]).Payload.ToArray()));
        Assert.AreEqual(1, stream.EndCount, "the DATA frame carrying the last body byte ends the stream");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3UploadOfUnknownLength_EndsTheStreamAfterTheBody()
    {
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("201", ("content-length", "0"))));

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream))).ExecuteAsync(Http3Context(
            "https://example.com/up",
            new MemoryStream(),
            upload: new UnseekableStream("abc"u8.ToArray())));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        List<Http3Frame> frames = await RequestFramesAsync(stream);
        Assert.AreEqual("abc", Latin1(((Http3DataFrame)frames[1]).Payload.ToArray()));
        Assert.IsTrue(stream.IsEndedByClient);
        Assert.AreEqual(1, stream.EndCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ResponseWithTrailers_WritesThemAfterTheBodyWithNoEmptyLine()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            FakeMultiplexedStream stream = new(
                0,
                Http3Response(Http3Head("200", ("content-type", "text/plain"), ("content-length", "5")), Http3Data("hello"), Http3Head(null, ("x-checksum", "abc"), ("x-second", "two"))),
                chunkSize);
            MemoryStream output = new();

            TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream)))
                .ExecuteAsync(Http3Context("https://example.com/", output, output));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(
                "HTTP/3 200 \r\ncontent-type: text/plain\r\ncontent-length: 5\r\n\r\nhellox-checksum: abc\r\nx-second: two\r\n",
                Latin1(output.ToArray()));
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3InterimHead_WritesItBeforeTheFinalOne()
    {
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("103", ("link", "</a>")), Http3Head("200"), Http3Data("x")));
        MemoryStream output = new();

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream)))
            .ExecuteAsync(Http3Context("https://example.com/", output, output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("HTTP/3 103 \r\nlink: </a>\r\n\r\nHTTP/3 200 \r\n\r\nx", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3StreamResetMidBody_FailsWithExit18AndCurlsMessage()
    {
        // cf-ngtcp2.c at curl-8_21_0: "HTTP/3 stream %d reset by server (error 0x%x %s)", CURLE_PARTIAL_FILE once body bytes arrived (ADR-0187).
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200", ("content-length", "10")), Http3Data("hello")))
        {
            EndException = new MultiplexedStreamResetException(0x10c, "reset"),
        };
        MemoryStream output = new();

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream)))
            .ExecuteAsync(Http3Context("https://example.com/", output));

        Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode);
        Assert.AreEqual("HTTP/3 stream 0 reset by server (error 0x10c REQUEST_CANCELLED)", result.ErrorMessage);
        Assert.AreEqual("hello", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3StreamResetBeforeAnyBody_FailsWithExit95()
    {
        FakeMultiplexedStream stream = new(4, Http3Response(Http3Head("200", ("content-length", "10"))))
        {
            EndException = new MultiplexedStreamResetException(0x10c, "reset"),
        };

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream)))
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Http3, result.ExitCode);
        Assert.AreEqual("HTTP/3 stream 4 reset by server (error 0x10c REQUEST_CANCELLED)", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3StreamEndedBeforeTheHead_FailsWithExit95()
    {
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("100")));

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream)))
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Http3, result.ExitCode);
        Assert.AreEqual("HTTP/3 stream 0 was closed cleanly, but before getting all response header fields, treated as error", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3HeadWithoutStatus_ResetsTheStreamWithMessageErrorAndFailsWithExit95()
    {
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head(null, ("content-length", "0"))));

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream)))
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Http3, result.ExitCode);
        Assert.AreEqual("HTTP/3 stream 0 reset by server (error 0x10e MESSAGE_ERROR)", result.ErrorMessage);
        Assert.AreEqual(0x10eL, stream.AbortCode, "H3_MESSAGE_ERROR");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3StreamRefusedBeforeAnyResponse_SendsTheRequestAgainOnANewQuicConnection()
    {
        // cf-ngtcp2.c and Curl_retry_request at curl-8_21_0 (ADR-0187): the refused line, then the
        // retry on a fresh connect, which needs no --retry.
        FakeMultiplexedStream refused = new(0, []) { EndException = new MultiplexedStreamResetException(0x10b, "refused") };
        FakeMultiplexedStream answered = new(0, Http3Response(Http3Head("200", ("content-length", "2")), Http3Data("ok")));
        FakeMultiplexedConnection first = new(refused);
        FakeMultiplexedConnection second = new(answered);
        QueueConnector connector = QuicConnector(first, second);
        MemoryStream output = new();
        RecordingTransferEvents events = new();

        TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
            "https://example.com/",
            output,
            options: new HttpRequestOptions { Body = new BytesBody("a=b"u8.ToArray(), "application/x-www-form-urlencoded") },
            events: events));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
        Assert.HasCount(2, connector.MultiplexedTargets);
        Assert.IsTrue(first.IsDisposed, "the refusing connection is closed");
        Assert.AreEqual("a=b", Latin1(((Http3DataFrame)(await RequestFramesAsync(answered))[1]).Payload.ToArray()), "the body is sent again");
        Assert.AreEqual(
            string.Join(
                "\n",
                "using HTTP/3",
                "upload completely sent off: 3 bytes",
                "HTTP/3 stream 0 refused by server, try again on a new connection",
                "REFUSED_STREAM, retrying a fresh connect",
                "Connection died, retrying a fresh connect (retry count: 1)",
                "shutting down connection #0",
                "Issue another request to this URL: 'https://example.com/'",
                "using HTTP/3",
                "upload completely sent off: 3 bytes",
                "Connection #0 to host example.com:443 left intact"),
            string.Join("\n", events.Info));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3StreamRefusedEveryTime_GivesUpAfterFiveRetriesWithExit56()
    {
        FakeMultiplexedConnection[] connections = [.. Enumerable.Range(0, 7).Select(_ => new FakeMultiplexedConnection(
            new FakeMultiplexedStream(0, []) { EndException = new MultiplexedStreamResetException(0x10b, "refused") }))];
        QueueConnector connector = QuicConnector(connections);
        RecordingTransferEvents events = new();

        TransferResult result = await Handler(connector).ExecuteAsync(Http3Context("https://example.com/", new MemoryStream(), events: events));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Connection died, tried 5 times before giving up", result.ErrorMessage);
        Assert.HasCount(6, connector.MultiplexedTargets, "the first attempt and five retries");
        CollectionAssert.AreEqual(
            new[] { 1, 2, 3, 4, 5 },
            events.Info.Where(line => line.StartsWith("Connection died, retrying", StringComparison.Ordinal)).Select(line => line[^2] - '0').ToArray());
        Assert.AreEqual(6, events.Info.Count(line => line == "REFUSED_STREAM, retrying a fresh connect"));
        Assert.IsTrue(connections.Take(6).All(connection => connection.IsDisposed));
    }

    [TestMethod]
    [DataRow("head", DisplayName = "after the response head")]
    [DataRow("stream-body", DisplayName = "with a body read from a stream")]
    public async Task ExecuteAsync_Http3StreamRefusedOnceTheResponseBeganOrWithAStreamBody_FailsWithExit56WithoutRetrying(string kind)
    {
        FakeMultiplexedStream refused = new(0, kind == "head" ? Http3Response(Http3Head("200", ("content-length", "5"))) : [])
        {
            EndException = new MultiplexedStreamResetException(0x10b, "refused"),
        };
        QueueConnector connector = QuicConnector(new FakeMultiplexedConnection(refused));
        RecordingTransferEvents events = new();

        TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
            "https://example.com/",
            new MemoryStream(),
            upload: kind == "head" ? null : new UnseekableStream("abc"u8.ToArray()),
            events: events));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", result.ErrorMessage);
        Assert.HasCount(1, connector.MultiplexedTargets);
        CollectionAssert.Contains(events.Info, "HTTP/3 stream 0 refused by server, try again on a new connection");
        CollectionAssert.DoesNotContain(events.Info, "REFUSED_STREAM, retrying a fresh connect");
    }

    [TestMethod]
    [DataRow(0x10cL, DisplayName = "H3_REQUEST_CANCELLED")]
    [DataRow(0x100L, DisplayName = "H3_NO_ERROR")]
    public async Task ExecuteAsync_Http3HeadRequestResetAfterTheHead_EndsWithExit0(long errorCode)
    {
        // curl-8_21_0's recv_closed_stream ignores a reset after the whole head when no body is wanted.
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200", ("content-length", "5"))))
        {
            EndException = new MultiplexedStreamResetException(errorCode, "reset"),
        };
        MemoryStream headerOutput = new();

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream)))
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream(), headerOutput, noBody: true));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("HTTP/3 200 \r\ncontent-length: 5\r\n\r\n", Latin1(headerOutput.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3StreamResetWithNoErrorBeforeTheHead_FailsAsAStreamClosedBeforeItsHead()
    {
        FakeMultiplexedStream stream = new(0, []) { EndException = new MultiplexedStreamResetException(0x100, "closed") };

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream)))
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Http3, result.ExitCode);
        Assert.AreEqual("HTTP/3 stream 0 was closed cleanly, but before getting all response header fields, treated as error", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("data-first", "ERR_H3_FRAME_UNEXPECTED", DisplayName = "DATA before HEADERS")]
    [DataRow("settings", "ERR_H3_FRAME_UNEXPECTED", DisplayName = "SETTINGS on a request stream")]
    [DataRow("truncated", "ERR_H3_FRAME_ERROR", DisplayName = "stream ends inside a frame")]
    [DataRow("http2-type", "ERR_H3_FRAME_UNEXPECTED", DisplayName = "reserved HTTP/2 frame type")]
    [DataRow("too-large", "ERR_H3_EXCESSIVE_LOAD", DisplayName = "DATA frame over the 16 MiB limit")]
    [DataRow("dynamic-reference", "ERR_QPACK_DECOMPRESSION_FAILED", DisplayName = "field section refers to the dynamic table")]
    public async Task ExecuteAsync_Http3FramesBreakingTheRfc_FailWithExit56AndNghttp3sErrorName(string kind, string errorName)
    {
        byte[] response = kind switch
        {
            "data-first" => Http3Response(Http3Data("x")),
            "settings" => new Http3SettingsFrame([]).ToBytes(),
            "truncated" => [0x00, 0x05, 0x61],
            "http2-type" => [0x06, 0x00],
            "too-large" => [0x00, 0x81, 0x00, 0x00, 0x01],
            _ => new Http3HeadersFrame(new byte[] { 0x02, 0x00, 0x80 }).ToBytes(),
        };
        FakeMultiplexedStream stream = new(0, response);

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream)))
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual($"nghttp3_conn_read_stream returned error: {errorName}", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ConnectionLostWhileReading_FailsWithTheConnectionsExitAndMessage()
    {
        FakeMultiplexedStream stream = new(0, [])
        {
            EndException = new MultiplexedConnectionFailedException(CurlExitCode.RecvError, "QUIC: connection lost"),
        };

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream)))
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("QUIC: connection lost", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ConnectionLostOpeningTheStream_FailsWithTheConnectionsExitAndMessage()
    {
        FakeMultiplexedConnection quic = new()
        {
            OpenException = new MultiplexedConnectionFailedException(CurlExitCode.QuicConnectError, "QUIC connection lacks 3 uni streams to run HTTP/3"),
        };

        TransferResult result = await Handler(QuicConnector(quic)).ExecuteAsync(Http3Context("https://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.QuicConnectError, result.ExitCode);
        Assert.AreEqual("QUIC connection lacks 3 uni streams to run HTTP/3", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ConnectionLostSendingTheHead_FailsWithTheConnectionsExitAndMessage()
    {
        FakeMultiplexedStream stream = new(0, [])
        {
            WriteException = new MultiplexedConnectionFailedException(CurlExitCode.SendError, "ngtcp2_conn_writev_stream returned error: ERR_CLOSING"),
        };

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream)))
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("ngtcp2_conn_writev_stream returned error: ERR_CLOSING", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ConnectionAlreadyLostAtClose_StillDisposesIt()
    {
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200", ("content-length", "0"))));
        FakeMultiplexedConnection quic = new(stream) { CloseException = new MultiplexedConnectionFailedException(CurlExitCode.RecvError, "lost") };

        TransferResult result = await Handler(QuicConnector(quic)).ExecuteAsync(Http3Context("https://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(quic.CloseCode);
        Assert.IsTrue(quic.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3OnlyWithHttpUrl_FailsWithExit3BeforeConnecting()
    {
        // curl --http3-only http://127.0.0.1:<p>/ (measured, ADR-0144).
        QueueConnector connector = new();
        RecordingTransferEvents events = new();

        TransferResult result = await Handler(connector).ExecuteAsync(
            Http3Context("http://127.0.0.1:18731/", new MemoryStream(), events: events));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("HTTP/3 requested for non-HTTPS URL", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "HTTP/3 requested for non-HTTPS URL", "closing connection #-1" }, events.Info);
        Assert.IsEmpty(connector.Targets);
        Assert.IsEmpty(connector.MultiplexedTargets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3OnlyQuicFails_FailsWithTheQuicExitAndNeverTriesTcp()
    {
        QueueConnector connector = new();
        connector.MultiplexedResults.Enqueue(MultiplexedConnectResult.Failed(CurlExitCode.SendError, "ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT"));

        TransferResult result = await Handler(connector).ExecuteAsync(Http3Context("https://127.0.0.1:18731/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT", result.ErrorMessage);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3QuicFails_FallsBackToTcp()
    {
        ScriptedConnection tcp = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536);
        QueueConnector connector = QueueConnector.For(tcp);
        connector.MultiplexedResults.Enqueue(MultiplexedConnectResult.Failed(CurlExitCode.RecvError, "QUIC: recvfrom() unexpectedly returned -1"));
        MemoryStream output = new();

        TransferResult result = await Handler(connector).ExecuteAsync(
            Http3Context("https://127.0.0.1:18731/", output, version: HttpVersionPreference.Http3));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
        Assert.AreEqual(new Version(1, 1), result.Report!.HttpVersion);
        Assert.HasCount(1, connector.MultiplexedTargets);
        Assert.HasCount(1, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3QuicAndTcpFail_FailsWithTheQuicAttemptsExitAndMessage()
    {
        // curl --http3 with nothing on UDP or TCP: curl: (56) QUIC: recvfrom() ... (measured, ADR-0144).
        ConnectTimings timings = new(1, 2, null, null);
        QueueConnector connector = new(ConnectResult.Refused("Failed to connect to 127.0.0.1 port 18731 after 2015 ms: Could not connect to server", timings, connectionNumber: 3));
        connector.MultiplexedResults.Enqueue(MultiplexedConnectResult.Failed(CurlExitCode.RecvError, "QUIC: recvfrom() unexpectedly returned -1"));
        RecordingTransferEvents events = new();

        TransferResult result = await Handler(connector).ExecuteAsync(
            Http3Context("https://127.0.0.1:18731/", new MemoryStream(), events: events, version: HttpVersionPreference.Http3));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("QUIC: recvfrom() unexpectedly returned -1", result.ErrorMessage);
        Assert.AreEqual(timings, result.Report!.Timings!.Connect);
        CollectionAssert.Contains(events.Info, "closing connection #3");
    }

    [TestMethod]
    [DataRow("http://127.0.0.1:18731/", false, DisplayName = "http:// URL")]
    [DataRow("https://127.0.0.1:18731/", true, DisplayName = "through a proxy")]
    public async Task ExecuteAsync_Http3WithoutHttpsOrThroughAProxy_ConnectsOverTcpOnly(string url, bool throughProxy)
    {
        QueueConnector connector = QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n", 65536));

        TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
            url,
            new MemoryStream(),
            options: new HttpRequestOptions { ForwardProxy = throughProxy ? new ProxyEndpoint(ProxyKind.Socks5, "127.0.0.1", 1080, null) : null },
            version: HttpVersionPreference.Http3));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsEmpty(connector.MultiplexedTargets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ConnectPassesTheConnectTimeout_FailsWithExit28()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        StalledConnector connector = new();
        TransferContext context = Http3Context("https://10.255.255.1/", new MemoryStream(), time: time);

        Task<TransferResult> transfer = new HttpProtocolHandler(connector, new SilentAuthenticator()).ExecuteAsync(context).AsTask();
        await connector.Started;
        time.Advance(TimeSpan.FromMilliseconds(1000));
        TransferResult result = await transfer;

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 1000 milliseconds", result.ErrorMessage);
    }

    private static QueueConnector QuicConnector(params FakeMultiplexedConnection[] quicConnections)
    {
        QueueConnector connector = new();
        foreach (FakeMultiplexedConnection quic in quicConnections)
        {
            connector.MultiplexedResults.Enqueue(MultiplexedConnectResult.Connected(quic, null));
        }

        return connector;
    }

    private static TransferContext Http3Context(
        string url,
        Stream output,
        Stream? headerOutput = null,
        HttpRequestOptions? options = null,
        Stream? upload = null,
        RecordingTransferEvents? events = null,
        HttpVersionPreference version = HttpVersionPreference.Http3Only,
        TimeProvider? time = null,
        bool noBody = false) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            HeaderOutput = headerOutput,
            Upload = upload,
            NoBody = noBody,
            Events = (ITransferEvents?)events ?? NoTransferEvents.Instance,
            TimeProvider = time ?? TimeProvider.System,
            ConnectTimeout = TimeSpan.FromMilliseconds(1000),
            Http = (options ?? new HttpRequestOptions()) with { Version = version, UserAgent = MeasuredUserAgent },
        };

    /// <summary>
    /// Gives a HEADERS frame whose field section a server's QPACK encoder, with no dynamic
    /// table, writes for <paramref name="status" /> and <paramref name="fields" />; no
    /// <c>:status</c> when <paramref name="status" /> is <see langword="null" />.
    /// </summary>
    private static Http3Frame Http3Head(string? status, params (string Name, string Value)[] fields)
    {
        List<HeaderField> lines = status is null ? [] : [new(":status", status)];
        lines.AddRange(fields.Select(field => new HeaderField(field.Name, field.Value)));
        return new Http3HeadersFrame(new QpackEncoder(0, 0).EncodeFieldSection(0, lines));
    }

    private static Http3Frame Http3Data(string text) => new Http3DataFrame(Encoding.Latin1.GetBytes(text));

    private static byte[] Http3Response(params Http3Frame[] frames) => [.. frames.SelectMany(frame => frame.ToBytes())];

    /// <summary>Reads the frames the client wrote on a request stream.</summary>
    private static async Task<List<Http3Frame>> RequestFramesAsync(FakeMultiplexedStream stream)
    {
        Http3FrameReader reader = new(new MemoryStream(stream.Written.ToArray()), 1 << 20);
        List<Http3Frame> frames = [];
        while (await reader.ReadFrameAsync(CancellationToken.None) is { } frame)
        {
            frames.Add(frame);
        }

        return frames;
    }

    /// <summary>Decodes the request's field section into <c>name: value</c> lines.</summary>
    private static async Task<string[]> RequestFieldsAsync(FakeMultiplexedStream stream)
    {
        Http3HeadersFrame headers = (Http3HeadersFrame)(await RequestFramesAsync(stream))[0];
        Assert.IsTrue(new QpackDecoder(0, 0).TryDecodeFieldSection(stream.StreamId, headers.EncodedFieldSection.Span, out IReadOnlyList<HeaderField>? fields));
        return [.. fields.Select(field => $"{field.Name}: {field.Value}")];
    }
}
