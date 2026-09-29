using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins the HTTP/2 exchange (BL-658) against curl.se's nghttp2 build of curl 8.18.0, whose
/// HTTP/2 layer is libcurl's own (ADR-0141): the request HEADERS blocks are the bytes that curl
/// sent with <c>-A curl/8.18.0</c>, and the response output is what it wrote for the same frames
/// (BL-658 Notes).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    /// <summary>The client preface, SETTINGS and connection WINDOW_UPDATE curl sent first.</summary>
    private const string Http2Preface =
        "505249202a20485454502f322e300d0a0d0a534d0d0a0d0a"
        + "000012040000000000" + "000300000064" + "000400010000" + "000200000000"
        + "000004080000000000" + "3e7f0001";

    private const string MeasuredUserAgent = "curl/8.18.0";

    [TestMethod]
    public async Task ExecuteAsync_Http2Get_SendsCurlsHeadersAndWritesTheHeadAndBody()
    {
        // curl -i --http2-prior-knowledge http://127.0.0.1:18658/a?b=1: HEADERS with END_STREAM and END_HEADERS.
        string headers = "000025010500000001"
            + "828641" + "8b089d5c0b8170dc0bce36f7" + "0485607fe47007" + "7a8825b650c3cb85e5c1" + "53032a2f2a";
        foreach (int chunkSize in ChunkSizes)
        {
            HpackEncoder server = new();
            byte[] response = Http2Response(
                Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-type", "text/plain"), new("content-length", "5")]), isEndStream: false, isEndHeaders: true),
                Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: true));
            ScriptedConnection connection = new(response, chunkSize, Convert.FromHexString(Http2Preface + headers));
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(Http2Context("http://127.0.0.1:18658/a?b=1", output, headerOutput));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("HTTP/2 200 \r\ncontent-type: text/plain\r\ncontent-length: 5\r\n\r\n", Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(200, result.Report!.ResponseCode);
            Assert.AreEqual(new Version(2, 0), result.Report.HttpVersion);
            Assert.IsFalse(connection.IsMarkedReusable, "an HTTP/2 connection is never pooled without its session");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2PostWithData_SendsCurlsHeadersThenOneDataFrameEndingTheStream()
    {
        // curl --http2-prior-knowledge -d name=value http://127.0.0.1:18659/form
        string headers = "000043010400000001"
            + "838641" + "8b089d5c0b8170dc0bce36ff" + "04846253d94f" + "7a8825b650c3cb85e5c1" + "53032a2f2a"
            + "0f0d023130" + "5f981d75d0620d263d4c795bc78f0b4a7b295adb282d443c8593";
        string data = "00000a000100000001" + Convert.ToHexString("name=value"u8);
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "2")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "ok"u8.ToArray(), isEndStream: true));
        ScriptedConnection connection = new(response, 65536, Convert.FromHexString(Http2Preface + headers + data));
        MemoryStream output = new();

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(Http2Context(
            "http://127.0.0.1:18659/form",
            output,
            options: new HttpRequestOptions { Body = new BytesBody("name=value"u8.ToArray(), "application/x-www-form-urlencoded") }));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
        Assert.AreEqual(10L, result.Report!.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2CustomHeaders_SendsThemLowerCasedWithoutConnectionSpecificOnes()
    {
        // curl --http2-prior-knowledge -H 'X-Custom: One' -H 'Accept: text/html' -H 'Connection: keep-alive'
        //      -H 'TE: trailers, gzip' -H 'Host: example.com' http://127.0.0.1:18660/
        string headers = "000037010500000001"
            + "828641" + "882f91d35d055c87a7" + "84" + "7a8825b650c3cb85e5c1"
            + "4086f2b12d424f4f034f6e65" + "5387497ca589d34d1f" + "40027465864d833505b11f";
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "2")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "ok"u8.ToArray(), isEndStream: true));
        ScriptedConnection connection = new(response, 65536, Convert.FromHexString(Http2Preface + headers));

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(Http2Context(
            "http://127.0.0.1:18660/",
            new MemoryStream(),
            options: new HttpRequestOptions { Headers = ["X-Custom: One", "Accept: text/html", "Connection: keep-alive", "TE: trailers, gzip", "Host: example.com"] }));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2ResponseWithTrailers_WritesThemAfterTheBodyWithNoEmptyLine()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            HpackEncoder server = new();
            byte[] response = Http2Response(
                Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-type", "text/plain")]), isEndStream: false, isEndHeaders: true),
                Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: false),
                Http2FrameFactory.CreateHeaders(1, server.Encode([new("x-checksum", "abc"), new("x-second", "two")]), isEndStream: true, isEndHeaders: true));
            MemoryStream output = new();

            TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, chunkSize)))
                .ExecuteAsync(Http2Context("http://127.0.0.1:18661/", output, output));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(
                "HTTP/2 200 \r\ncontent-type: text/plain\r\n\r\nhellox-checksum: abc\r\nx-second: two\r\n",
                Latin1(output.ToArray()),
                $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2TrailersAfterAContentLengthBody_AreStillWritten()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "5")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: false),
            Http2FrameFactory.CreateHeaders(1, server.Encode([new("x-checksum", "abc")]), isEndStream: true, isEndHeaders: true));
        MemoryStream output = new();
        MemoryStream headerOutput = new();

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", output, headerOutput));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", Latin1(output.ToArray()));
        StringAssert.EndsWith(Latin1(headerOutput.ToArray()), "\r\n\r\nx-checksum: abc\r\n");
    }

    [TestMethod]
    [DataRow(Http2ErrorCode.InternalError, "HTTP/2 stream 1 was not closed cleanly: INTERNAL_ERROR (err 2)", DisplayName = "INTERNAL_ERROR")]
    [DataRow(Http2ErrorCode.Cancel, "HTTP/2 stream 1 was not closed cleanly: CANCEL (err 8)", DisplayName = "CANCEL")]
    public async Task ExecuteAsync_Http2StreamResetMidBody_FailsWithExit92AfterTheBodySoFar(Http2ErrorCode errorCode, string message)
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "10")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: false),
            Http2FrameFactory.CreateRstStream(1, errorCode));
        MemoryStream output = new();
        MemoryStream headerOutput = new();

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://127.0.0.1:18662/", output, headerOutput));

        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual("HTTP/2 200 \r\ncontent-length: 10\r\n\r\n", Latin1(headerOutput.ToArray()));
        Assert.AreEqual("hello", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2GoAwayWithAnErrorMidBody_FailsWithExit56()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: false),
            Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.InternalError, ReadOnlyMemory<byte>.Empty));
        MemoryStream output = new();

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", output));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", result.ErrorMessage);
        Assert.AreEqual("hello", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2ProtocolError_FailsWithExit16AndSendsGoAway()
    {
        byte[] response = Http2Response(Http2FrameFactory.CreateContinuation(1, new byte[] { 0x88 }, isEndHeaders: true));
        ScriptedConnection connection = new(response, 65536);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual("nghttp2 shuts down connection with error 1: PROTOCOL_ERROR", result.ErrorMessage);
        Assert.AreEqual((byte)Http2FrameType.GoAway, connection.Written[^14]);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2PeerClosesMidBody_FailsWithExit18()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: false));
        MemoryStream output = new();

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", output));

        Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode);
        Assert.AreEqual("Transferred a partial file", result.ErrorMessage);
        Assert.AreEqual("hello", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2PeerClosesBeforeTheHead_FailsWithExit16()
    {
        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(Http2Response(), 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual("Error in the HTTP2 framing layer", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2PeerClosesInsideAFrame_FailsLikeAClose()
    {
        HpackEncoder server = new();
        byte[] whole = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: false));

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(whole[..^2], 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no :status")]
    [DataRow("2x0", DisplayName = "not digits")]
    public async Task ExecuteAsync_Http2HeadWithoutAValidStatus_ResetsTheStreamAndFailsWithExit92(string? status)
    {
        HpackEncoder server = new();
        HeaderField[] fields = status is null ? [new("content-type", "text/plain")] : [new(":status", status)];
        byte[] response = Http2Response(Http2FrameFactory.CreateHeaders(1, server.Encode(fields), isEndStream: true, isEndHeaders: true));
        ScriptedConnection connection = new(response, 65536);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual("HTTP/2 stream 1 was not closed cleanly: PROTOCOL_ERROR (err 1)", result.ErrorMessage);
        Assert.AreEqual("00000403000000000100000001", Convert.ToHexString(connection.Written[^13..]), "RST_STREAM PROTOCOL_ERROR");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2DataBeforeTheHead_ResetsTheStreamAndFailsWithExit92()
    {
        byte[] response = Http2Response(Http2FrameFactory.CreateData(1, "x"u8.ToArray(), isEndStream: true));

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual("HTTP/2 stream 1 was not closed cleanly: PROTOCOL_ERROR (err 1)", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2UndecodableHeaderBlock_FailsWithExit16CompressionError()
    {
        byte[] response = Http2Response(Http2FrameFactory.CreateHeaders(1, new byte[] { 0x80 }, isEndStream: true, isEndHeaders: true));

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual("nghttp2 shuts down connection with error 9: COMPRESSION_ERROR", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2InformationalHead_IsWrittenBeforeTheFinalOne()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "103"), new("link", "</a>")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "204")]), isEndStream: true, isEndHeaders: true));
        MemoryStream headerOutput = new();

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream(), headerOutput));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("HTTP/2 103 \r\nlink: </a>\r\n\r\nHTTP/2 204 \r\n\r\n", Latin1(headerOutput.ToArray()));
        Assert.AreEqual(204, result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_H2AgreedWithAlpn_SpeaksHttp2AndReportsIt()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "204")]), isEndStream: true, isEndHeaders: true));
        ScriptedConnection connection = new(response, 65536);
        RecordingTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse("https://example.com/"), Output = new MemoryStream(), Events = events };

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, applicationProtocol: "h2")))
            .ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.StartsWith(Latin1(connection.Written), "PRI * HTTP/2.0");
        CollectionAssert.Contains(events.Info, "using HTTP/2");
        Assert.AreEqual("> GET / HTTP/2\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n", events.Events.First(line => line.StartsWith("> ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http11AgreedWithAlpn_SpeaksHttp11()
    {
        ScriptedConnection connection = Connection(NoContent, 65536, RootRequest);

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, applicationProtocol: "http/1.1")))
            .ExecuteAsync(Context("https://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2UploadBeyondTheWindow_WaitsForWindowUpdatesThenEndsTheStream()
    {
        byte[] body = new byte[70000];
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateWindowUpdate(0, 10000),
            Http2FrameFactory.CreateWindowUpdate(1, 10000),
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "201")]), isEndStream: true, isEndHeaders: true));
        ScriptedConnection connection = new(response, 65536);

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(Http2Context(
            "http://example.com/up",
            new MemoryStream(),
            options: new HttpRequestOptions { Body = new BytesBody(body, "application/octet-stream") }));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(70000L, result.Report!.UploadSize);
        List<Http2Frame> sent = await FramesAfterPreface(connection.Written);
        Http2Frame[] data = [.. sent.Where(frame => frame.Type == Http2FrameType.Data)];
        Assert.AreEqual(70000, data.Sum(frame => frame.Payload.Length));
        Assert.AreEqual(Http2FrameFlags.EndStream, data[^1].Flags);
        Assert.IsTrue(data[..^1].All(frame => frame.Flags == 0));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2ResponseEndingWhileTheBodyWaitsForWindow_StopsSending()
    {
        byte[] body = new byte[70000];
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "413")]), isEndStream: true, isEndHeaders: true));
        ScriptedConnection connection = new(response, 65536);
        MemoryStream headerOutput = new();

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(Http2Context(
            "http://example.com/up",
            new MemoryStream(),
            headerOutput,
            new HttpRequestOptions { Body = new BytesBody(body, "application/octet-stream") }));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("HTTP/2 413 \r\n\r\n", Latin1(headerOutput.ToArray()));
        List<Http2Frame> sent = await FramesAfterPreface(connection.Written);
        Assert.AreEqual(65535, sent.Where(frame => frame.Type == Http2FrameType.Data).Sum(frame => frame.Payload.Length));
        Assert.IsFalse(sent.Any(frame => frame.Type == Http2FrameType.Data && frame.Flags == Http2FrameFlags.EndStream));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2UploadOfUnknownLength_EndsTheStreamWithAnEmptyDataFrame()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "201")]), isEndStream: true, isEndHeaders: true));
        ScriptedConnection connection = new(response, 65536);
        TransferContext context = Http2Context("http://example.com/up", new MemoryStream(), upload: new UnseekableStream("abc"u8.ToArray()));

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        List<Http2Frame> sent = await FramesAfterPreface(connection.Written);
        Http2Frame headers = sent.Single(frame => frame.Type == Http2FrameType.Headers);
        Assert.AreEqual(Http2FrameFlags.EndHeaders, headers.Flags, "HEADERS without END_STREAM");
        IReadOnlyList<HeaderField> fields = new HpackDecoder().Decode(headers.Payload.Span);
        Assert.IsFalse(fields.Any(field => field.Name is "transfer-encoding" or "expect" or "content-length"));
        Http2Frame[] data = [.. sent.Where(frame => frame.Type == Http2FrameType.Data)];
        Assert.AreEqual("abc", Latin1(data[0].Payload.ToArray()));
        Assert.AreEqual(0, data[1].Payload.Length);
        Assert.AreEqual(Http2FrameFlags.EndStream, data[1].Flags);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2StreamEndFails_FailsWithExit55()
    {
        TransferContext context = Http2Context("http://example.com/up", new MemoryStream(), upload: new UnseekableStream("abc"u8.ToArray()));

        TransferResult result = await Handler(QueueConnector.For(new FailingSendConnection(new IOException("reset"), writesBeforeFailure: 3)))
            .ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2AuthenticationRetry_GoesOnTheNextStreamOfTheSameConnection()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateSettings([new(Http2SettingIdentifier.HeaderTableSize, 0)]),
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "401"), new("www-authenticate", "Basic realm=\"r\""), new("content-length", "0")]), isEndStream: true, isEndHeaders: true),
            Http2FrameFactory.CreateHeaders(1, server.Encode([new("x-stray", "late")]), isEndStream: true, isEndHeaders: true),
            Http2FrameFactory.CreateHeaders(3, server.Encode([new(":status", "200"), new("content-length", "2")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(3, "ok"u8.ToArray(), isEndStream: true));
        ScriptedConnection connection = new(response, 65536);
        ScriptedAuthenticator authenticator = new(null, "Basic dTpw");
        MemoryStream output = new();

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator)
            .ExecuteAsync(Http2Context("http://example.com/", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
        Assert.AreEqual(1, result.Report!.ConnectionCount);
        Http2Frame[] headers = [.. (await FramesAfterPreface(connection.Written)).Where(frame => frame.Type == Http2FrameType.Headers)];
        Assert.AreEqual(3, headers[1].StreamId);
        Assert.AreEqual(0x20, headers[1].Payload.Span[0], "the retry's block opens with the table size update the peer's SETTINGS asked for");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2RetryAfterGoAway_GoesOnANewConnection()
    {
        HpackEncoder first = new();
        byte[] refused = Http2Response(
            Http2FrameFactory.CreateGoAway(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty),
            Http2FrameFactory.CreateHeaders(1, first.Encode([new(":status", "401"), new("www-authenticate", "Basic realm=\"r\""), new("content-length", "0")]), isEndStream: true, isEndHeaders: true));
        HpackEncoder second = new();
        byte[] accepted = Http2Response(Http2FrameFactory.CreateHeaders(1, second.Encode([new(":status", "204")]), isEndStream: true, isEndHeaders: true));
        ScriptedAuthenticator authenticator = new(null, "Basic dTpw");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(refused, 65536), new ScriptedConnection(accepted, 65536)), authenticator)
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(2, result.Report!.ConnectionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2RetryWhenThePeerAllowsNoStream_FailsWithExit16()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateSettings([new(Http2SettingIdentifier.MaxConcurrentStreams, 0)]),
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "401"), new("www-authenticate", "Basic realm=\"r\""), new("content-length", "0")]), isEndStream: true, isEndHeaders: true));
        ScriptedAuthenticator authenticator = new(null, "Basic dTpw");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(response, 65536)), authenticator)
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual("Error in the HTTP2 framing layer", result.ErrorMessage);
    }

    private static TransferContext Http2Context(string url, Stream output, Stream? headerOutput = null, HttpRequestOptions? options = null, Stream? upload = null) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            HeaderOutput = headerOutput,
            Upload = upload,
            Http = (options ?? new HttpRequestOptions()) with { Version = HttpVersionPreference.Http2PriorKnowledge, UserAgent = (options?.UserAgent) ?? MeasuredUserAgent },
        };

    /// <summary>
    /// Gives the bytes a server sends: its empty SETTINGS and the acknowledgement of the
    /// client's, then <paramref name="frames" />.
    /// </summary>
    private static byte[] Http2Response(params Http2Frame[] frames) =>
    [
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
        .. frames.SelectMany(Http2FrameCodec.Serialize),
    ];

    /// <summary>Reads the frames the client wrote after its preface.</summary>
    private static async Task<List<Http2Frame>> FramesAfterPreface(byte[] written)
    {
        using MemoryStream input = new(written, Http2Connection.ClientPreface.Length, written.Length - Http2Connection.ClientPreface.Length);
        List<Http2Frame> frames = [];
        while (await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.LargestMaximumFrameSize, CancellationToken.None) is { } frame)
        {
            frames.Add(frame);
        }

        return frames;
    }
}
