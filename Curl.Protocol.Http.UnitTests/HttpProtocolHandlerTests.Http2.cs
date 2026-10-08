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

    /// <summary>
    /// The two WINDOW_UPDATEs curl 8.18.0 sends on stream 1 right after the frame that ends the
    /// request, growing its window to 10 MiB from the default 65535 (measured, BL-817 Notes).
    /// </summary>
    private const string StreamOneWindowUpdates = "000004080000000001009F0001" + "000004080000000001009F0001";

    /// <summary>
    /// The GOAWAY curl 8.18.0 sends when it closes an HTTP/2 connection: last stream 0,
    /// NO_ERROR, debug data <c>shutdown</c> and a NUL (measured, BL-817 Notes).
    /// </summary>
    private const string ClosingGoAway = "000011070000000000" + "00000000" + "00000000" + "73687574646F776E00";

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
            ScriptedConnection connection = new(response, chunkSize, Convert.FromHexString(Http2Preface + headers + StreamOneWindowUpdates));
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            Diagnostics.Arrange("url, chunk size", $"http://127.0.0.1:18658/a?b=1, {chunkSize}");
            Diagnostics.Arrange("response frames", "HEADERS 200 text/plain length 5, DATA hello (end stream)");

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(Http2Context("http://127.0.0.1:18658/a?b=1", output, headerOutput));

            WriteResult(result);
            Diagnostics.Act("response code, http version", $"{result.Report?.ResponseCode}, {result.Report?.HttpVersion}");
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Diff("head", OneLine("HTTP/2 200 \r\ncontent-type: text/plain\r\ncontent-length: 5\r\n\r\n"), OneLine(Latin1(headerOutput.ToArray())));
            Assert.AreEqual("HTTP/2 200 \r\ncontent-type: text/plain\r\ncontent-length: 5\r\n\r\n", Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Diagnostics.Assert("body", "hello", Latin1(output.ToArray()));
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Diagnostics.Assert("response code", 200, result.Report!.ResponseCode);
            Assert.AreEqual(200, result.Report!.ResponseCode);
            Diagnostics.Assert("http version", new Version(2, 0), result.Report.HttpVersion);
            Assert.AreEqual(new Version(2, 0), result.Report.HttpVersion);
            Diagnostics.Assert("connection marked reusable", true, connection.IsMarkedReusable);
            Assert.IsTrue(connection.IsMarkedReusable, $"Chunk size {chunkSize}: curl leaves an HTTP/2 connection intact");
            Diagnostics.Assert("written ends with closing goaway", true, Convert.ToHexString(connection.Written).EndsWith(ClosingGoAway, StringComparison.Ordinal));
            StringAssert.EndsWith(Convert.ToHexString(connection.Written), ClosingGoAway, $"Chunk size {chunkSize}: a connection that holds no session gets the GOAWAY from the handler");
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
        ScriptedConnection connection = new(response, 65536, Convert.FromHexString(Http2Preface + headers + data + StreamOneWindowUpdates));
        MemoryStream output = new();

        Diagnostics.Arrange("url, body", "http://127.0.0.1:18659/form, name=value (application/x-www-form-urlencoded)");
        Diagnostics.Arrange("response frames", "HEADERS 200 length 2, DATA ok (end stream)");

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(Http2Context(
            "http://127.0.0.1:18659/form",
            output,
            options: new HttpRequestOptions { Body = new BytesBody("name=value"u8.ToArray(), "application/x-www-form-urlencoded") }));

        WriteResult(result);
        Diagnostics.Act("upload size", result.Report?.UploadSize);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("body", "ok", Latin1(output.ToArray()));
        Assert.AreEqual("ok", Latin1(output.ToArray()));
        Diagnostics.Assert("upload size", 10L, result.Report!.UploadSize);
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
        ScriptedConnection connection = new(response, 65536, Convert.FromHexString(Http2Preface + headers + StreamOneWindowUpdates));

        Diagnostics.Arrange("url", "http://127.0.0.1:18660/");
        Diagnostics.Arrange("custom headers", "X-Custom: One | Accept: text/html | Connection: keep-alive | TE: trailers, gzip | Host: example.com");

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(Http2Context(
            "http://127.0.0.1:18660/",
            new MemoryStream(),
            options: new HttpRequestOptions { Headers = ["X-Custom: One", "Accept: text/html", "Connection: keep-alive", "TE: trailers, gzip", "Host: example.com"] }));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
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

            Diagnostics.Arrange("url, chunk size", $"http://127.0.0.1:18661/, {chunkSize}");
            Diagnostics.Arrange("response frames", "HEADERS 200 text/plain, DATA hello, HEADERS trailers x-checksum x-second (end stream)");

            TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, chunkSize)))
                .ExecuteAsync(Http2Context("http://127.0.0.1:18661/", output, output));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Diff(
                "output",
                OneLine("HTTP/2 200 \r\ncontent-type: text/plain\r\n\r\nhellox-checksum: abc\r\nx-second: two\r\n"),
                OneLine(Latin1(output.ToArray())));
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

        Diagnostics.Arrange("url", "http://example.com/");
        Diagnostics.Arrange("response frames", "HEADERS 200 length 5, DATA hello, HEADERS trailer x-checksum (end stream)");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", output, headerOutput));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("body", "hello", Latin1(output.ToArray()));
        Assert.AreEqual("hello", Latin1(output.ToArray()));
        Diagnostics.Assert("head output ends with trailer", true, Latin1(headerOutput.ToArray()).EndsWith("\r\n\r\nx-checksum: abc\r\n", StringComparison.Ordinal));
        Diagnostics.Act("head output", OneLine(Latin1(headerOutput.ToArray())));
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

        Diagnostics.Arrange("url, error code", $"http://127.0.0.1:18662/, {errorCode}");
        Diagnostics.Arrange("response frames", "HEADERS 200 length 10, DATA hello, RST_STREAM");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://127.0.0.1:18662/", output, headerOutput));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        Diagnostics.Assert("error message", message, result.ErrorMessage);
        Assert.AreEqual(message, result.ErrorMessage);
        Diagnostics.Diff("head", OneLine("HTTP/2 200 \r\ncontent-length: 10\r\n\r\n"), OneLine(Latin1(headerOutput.ToArray())));
        Assert.AreEqual("HTTP/2 200 \r\ncontent-length: 10\r\n\r\n", Latin1(headerOutput.ToArray()));
        Diagnostics.Assert("body", "hello", Latin1(output.ToArray()));
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

        Diagnostics.Arrange("url", "http://example.com/");
        Diagnostics.Arrange("response frames", "HEADERS 200, DATA hello, GOAWAY INTERNAL_ERROR");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", output));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Diagnostics.Assert("error message", "Failure when receiving data from the peer", result.ErrorMessage);
        Assert.AreEqual("Failure when receiving data from the peer", result.ErrorMessage);
        Diagnostics.Assert("body", "hello", Latin1(output.ToArray()));
        Assert.AreEqual("hello", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2ProtocolError_FailsWithExit16AndSendsGoAway()
    {
        byte[] response = Http2Response(Http2FrameFactory.CreateContinuation(1, new byte[] { 0x88 }, isEndHeaders: true));
        ScriptedConnection connection = new(response, 65536);

        Diagnostics.Arrange("url", "http://example.com/");
        Diagnostics.Arrange("response frames", "CONTINUATION on stream 1 without a HEADERS");

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Diagnostics.Assert("error message", "nghttp2 shuts down connection with error 1: PROTOCOL_ERROR", result.ErrorMessage);
        Assert.AreEqual("nghttp2 shuts down connection with error 1: PROTOCOL_ERROR", result.ErrorMessage);
        Diagnostics.Assert("frame type fourteen bytes from the end", (byte)Http2FrameType.GoAway, connection.Written[^14]);
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

        Diagnostics.Arrange("url", "http://example.com/");
        Diagnostics.Arrange("response frames", "HEADERS 200, DATA hello, then the peer closes");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", output));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.PartialFile, result.ExitCode);
        Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode);
        Diagnostics.Assert("error message", "Transferred a partial file", result.ErrorMessage);
        Assert.AreEqual("Transferred a partial file", result.ErrorMessage);
        Diagnostics.Assert("body", "hello", Latin1(output.ToArray()));
        Assert.AreEqual("hello", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2PeerClosesBeforeTheHead_FailsWithExit16()
    {
        Diagnostics.Arrange("url", "http://example.com/");
        Diagnostics.Arrange("response frames", "SETTINGS and acknowledgement only, then the peer closes");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(Http2Response(), 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Diagnostics.Assert("error message", "Error in the HTTP2 framing layer", result.ErrorMessage);
        Assert.AreEqual("Error in the HTTP2 framing layer", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2PeerClosesInsideAFrame_FailsLikeAClose()
    {
        HpackEncoder server = new();
        byte[] whole = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: false));

        Diagnostics.Arrange("url", "http://example.com/");
        Diagnostics.Arrange("response frames", $"HEADERS 200, DATA hello cut two bytes short, total {whole.Length - 2} bytes");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(whole[..^2], 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.PartialFile, result.ExitCode);
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

        Diagnostics.Arrange("url, status", $"http://example.com/, {status ?? "(none)"}");

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        Diagnostics.Assert("error message", "HTTP/2 stream 1 was not closed cleanly: PROTOCOL_ERROR (err 1)", result.ErrorMessage);
        Assert.AreEqual("HTTP/2 stream 1 was not closed cleanly: PROTOCOL_ERROR (err 1)", result.ErrorMessage);
        Diagnostics.Assert("written ends with reset then goaway", true, Convert.ToHexString(connection.Written).EndsWith("00000403000000000100000001" + ClosingGoAway, StringComparison.Ordinal));
        StringAssert.EndsWith(Convert.ToHexString(connection.Written), "00000403000000000100000001" + ClosingGoAway, "RST_STREAM PROTOCOL_ERROR, then the closing GOAWAY");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2DataBeforeTheHead_ResetsTheStreamAndFailsWithExit92()
    {
        byte[] response = Http2Response(Http2FrameFactory.CreateData(1, "x"u8.ToArray(), isEndStream: true));

        Diagnostics.Arrange("url", "http://example.com/");
        Diagnostics.Arrange("response frames", "DATA x on stream 1 before any HEADERS");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        Diagnostics.Assert("error message", "HTTP/2 stream 1 was not closed cleanly: PROTOCOL_ERROR (err 1)", result.ErrorMessage);
        Assert.AreEqual("HTTP/2 stream 1 was not closed cleanly: PROTOCOL_ERROR (err 1)", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2UndecodableHeaderBlock_FailsWithExit16CompressionError()
    {
        byte[] response = Http2Response(Http2FrameFactory.CreateHeaders(1, new byte[] { 0x80 }, isEndStream: true, isEndHeaders: true));

        Diagnostics.Arrange("url", "http://example.com/");
        Diagnostics.Arrange("response frames", "HEADERS with the undecodable block 0x80");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Diagnostics.Assert("error message", "nghttp2 shuts down connection with error 9: COMPRESSION_ERROR", result.ErrorMessage);
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

        Diagnostics.Arrange("url", "http://example.com/");
        Diagnostics.Arrange("response frames", "HEADERS 103 link, HEADERS 204 (end stream)");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream(), headerOutput));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("head", OneLine("HTTP/2 103 \r\nlink: </a>\r\n\r\nHTTP/2 204 \r\n\r\n"), OneLine(Latin1(headerOutput.ToArray())));
        Assert.AreEqual("HTTP/2 103 \r\nlink: </a>\r\n\r\nHTTP/2 204 \r\n\r\n", Latin1(headerOutput.ToArray()));
        Diagnostics.Assert("response code", 204, result.Report!.ResponseCode);
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

        Diagnostics.Arrange("url, alpn", "https://example.com/, h2");
        Diagnostics.Arrange("response frames", "HEADERS 204 (end stream)");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, applicationProtocol: "h2")))
            .ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("info", events.Info);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("written starts with", "PRI * HTTP/2.0", Latin1(connection.Written).Substring(0, 14));
        StringAssert.StartsWith(Latin1(connection.Written), "PRI * HTTP/2.0");
        Diagnostics.Assert("info contains", "using HTTP/2", events.Info.Contains("using HTTP/2") ? "using HTTP/2" : "(missing)");
        CollectionAssert.Contains(events.Info, "using HTTP/2");
        Diagnostics.Assert(
            "request head event",
            OneLine("> GET / HTTP/2\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n"),
            OneLine(events.Events.First(line => line.StartsWith("> ", StringComparison.Ordinal))));
        Assert.AreEqual("> GET / HTTP/2\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n", events.Events.First(line => line.StartsWith("> ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2Stream_ReportsItOpenedAndEachHeaderBeforeTheRequestHead()
    {
        // curl -v --http2 https://example.com/ with curl.se's nghttp2 build (BL-660 Notes).
        HpackEncoder server = new();
        byte[] response = Http2Response(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "204")]), isEndStream: true, isEndHeaders: true));
        RecordingTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse("HTTPS://example.com?x#frag"), Output = new MemoryStream(), Events = events };

        Diagnostics.Arrange("url, alpn", "HTTPS://example.com?x#frag, h2");
        Diagnostics.Arrange("response frames", "HEADERS 204 (end stream)");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(new ScriptedConnection(response, 65536), null, applicationProtocol: "h2")))
            .ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        int opened = events.Events.IndexOf("* [HTTP/2] [1] OPENED stream for https://example.com/?x#frag");
        Diagnostics.Act("opened event index", opened);
        WriteExpectedLines(
            "events from opened",
            [
                "* [HTTP/2] [1] OPENED stream for https://example.com/?x#frag",
                "* [HTTP/2] [1] [:method: GET]",
                "* [HTTP/2] [1] [:scheme: https]",
                "* [HTTP/2] [1] [:authority: example.com]",
                "* [HTTP/2] [1] [:path: /?x]",
                "* [HTTP/2] [1] [user-agent: curl/8.21.0]",
                "* [HTTP/2] [1] [accept: */*]",
                "> GET /?x HTTP/2\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            ],
            events.Events.Skip(opened).Take(8));
        CollectionAssert.AreEqual(
            new[]
            {
                "* [HTTP/2] [1] OPENED stream for https://example.com/?x#frag",
                "* [HTTP/2] [1] [:method: GET]",
                "* [HTTP/2] [1] [:scheme: https]",
                "* [HTTP/2] [1] [:authority: example.com]",
                "* [HTTP/2] [1] [:path: /?x]",
                "* [HTTP/2] [1] [user-agent: curl/8.21.0]",
                "* [HTTP/2] [1] [accept: */*]",
                "> GET /?x HTTP/2\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            },
            events.Events.Skip(opened).Take(8).ToArray());
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public async Task ExecuteAsync_Http2ConnectionLeftIntact_ReportsItOnlyWhenNoOtherTransferSharesIt(bool isShared, bool reportsLeftIntact)
    {
        // curl -Z --http2-prior-knowledge -v with three URLs printed one "left intact", when the last stream ended (BL-717 Notes).
        HpackEncoder server = new();
        byte[] response = Http2Response(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "204")]), isEndStream: true, isEndHeaders: true));
        SessionHoldingConnection connection = new(new ScriptedConnection(response, 65536)) { IsSharedWithAnotherTransfer = isShared };
        RecordingTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream(), Events = events, Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge } };

        Diagnostics.Arrange("url, version", "http://example.com/, Http2PriorKnowledge");
        Diagnostics.Arrange("shared with another transfer, expected left intact", $"{isShared}, {reportsLeftIntact}");

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("info", events.Info);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("left intact reported", reportsLeftIntact, events.Info.Contains("Connection #0 to host example.com:80 left intact"));
        Assert.AreEqual(reportsLeftIntact, events.Info.Contains("Connection #0 to host example.com:80 left intact"));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http11AgreedWithAlpn_SpeaksHttp11()
    {
        ScriptedConnection connection = Connection(NoContent, 65536, RootRequest);

        Diagnostics.Arrange("url, alpn", "https://example.com/, http/1.1");
        Diagnostics.Arrange("expected request", OneLine(RootRequest));

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, applicationProtocol: "http/1.1")))
            .ExecuteAsync(Context("https://example.com/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
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

        Diagnostics.Arrange("url, body length", "http://example.com/up, 70000");
        Diagnostics.Arrange("response frames", "WINDOW_UPDATE 0 +10000, WINDOW_UPDATE 1 +10000, HEADERS 201 (end stream)");

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(Http2Context(
            "http://example.com/up",
            new MemoryStream(),
            options: new HttpRequestOptions { Body = new BytesBody(body, "application/octet-stream") }));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("upload size", 70000L, result.Report!.UploadSize);
        Assert.AreEqual(70000L, result.Report!.UploadSize);
        List<Http2Frame> sent = await FramesAfterPreface(connection.Written);
        Http2Frame[] data = [.. sent.Where(frame => frame.Type == Http2FrameType.Data)];
        Diagnostics.Act("frames sent, data frames", $"{sent.Count}, {data.Length}");
        Diagnostics.Assert("data payload total", 70000, data.Sum(frame => frame.Payload.Length));
        Assert.AreEqual(70000, data.Sum(frame => frame.Payload.Length));
        Diagnostics.Assert("last data flags", Http2FrameFlags.EndStream, data[^1].Flags);
        Assert.AreEqual(Http2FrameFlags.EndStream, data[^1].Flags);
        Diagnostics.Assert("earlier data frames all without flags", true, data[..^1].All(frame => frame.Flags == 0));
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

        Diagnostics.Arrange("url, body length", "http://example.com/up, 70000");
        Diagnostics.Arrange("response frames", "HEADERS 413 (end stream)");

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(Http2Context(
            "http://example.com/up",
            new MemoryStream(),
            headerOutput,
            new HttpRequestOptions { Body = new BytesBody(body, "application/octet-stream") }));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("head", OneLine("HTTP/2 413 \r\n\r\n"), OneLine(Latin1(headerOutput.ToArray())));
        Assert.AreEqual("HTTP/2 413 \r\n\r\n", Latin1(headerOutput.ToArray()));
        List<Http2Frame> sent = await FramesAfterPreface(connection.Written);
        Diagnostics.Act("frames sent", sent.Count);
        Diagnostics.Assert("data payload total", 65535, sent.Where(frame => frame.Type == Http2FrameType.Data).Sum(frame => frame.Payload.Length));
        Assert.AreEqual(65535, sent.Where(frame => frame.Type == Http2FrameType.Data).Sum(frame => frame.Payload.Length));
        Diagnostics.Assert("any data frame ends the stream", false, sent.Any(frame => frame.Type == Http2FrameType.Data && frame.Flags == Http2FrameFlags.EndStream));
        Assert.IsFalse(sent.Any(frame => frame.Type == Http2FrameType.Data && frame.Flags == Http2FrameFlags.EndStream));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2UploadOfUnknownLength_EndsTheStreamWithAnEmptyDataFrame()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "201")]), isEndStream: true, isEndHeaders: true));
        ScriptedConnection connection = new(response, 65536);
        TransferContext context = Http2Context("http://example.com/up", new MemoryStream(), upload: new UnseekableStream("abc"u8.ToArray()));

        Diagnostics.Arrange("url, upload", "http://example.com/up, abc from an unseekable stream");
        Diagnostics.Arrange("response frames", "HEADERS 201 (end stream)");

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        List<Http2Frame> sent = await FramesAfterPreface(connection.Written);
        Http2Frame headers = sent.Single(frame => frame.Type == Http2FrameType.Headers);
        Diagnostics.Act("frames sent", sent.Count);
        Diagnostics.Assert("headers flags", Http2FrameFlags.EndHeaders, headers.Flags);
        Assert.AreEqual(Http2FrameFlags.EndHeaders, headers.Flags, "HEADERS without END_STREAM");
        IReadOnlyList<HeaderField> fields = new HpackDecoder().Decode(headers.Payload.Span);
        Diagnostics.Act("request header names", string.Join(", ", fields.Select(field => field.Name)));
        Diagnostics.Assert("any transfer-encoding, expect or content-length field", false, fields.Any(field => field.Name is "transfer-encoding" or "expect" or "content-length"));
        Assert.IsFalse(fields.Any(field => field.Name is "transfer-encoding" or "expect" or "content-length"));
        Http2Frame[] data = [.. sent.Where(frame => frame.Type == Http2FrameType.Data)];
        Diagnostics.Assert("first data payload", "abc", Latin1(data[0].Payload.ToArray()));
        Assert.AreEqual("abc", Latin1(data[0].Payload.ToArray()));
        Diagnostics.Assert("second data length", 0, data[1].Payload.Length);
        Assert.AreEqual(0, data[1].Payload.Length);
        Diagnostics.Assert("second data flags", Http2FrameFlags.EndStream, data[1].Flags);
        Assert.AreEqual(Http2FrameFlags.EndStream, data[1].Flags);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2StreamEndFails_FailsWithExit55()
    {
        TransferContext context = Http2Context("http://example.com/up", new MemoryStream(), upload: new UnseekableStream("abc"u8.ToArray()));

        Diagnostics.Arrange("url, upload", "http://example.com/up, abc from an unseekable stream");
        Diagnostics.Arrange("failing send", "IOException reset after 3 writes");

        TransferResult result = await Handler(QueueConnector.For(new FailingSendConnection(new IOException("reset"), writesBeforeFailure: 3)))
            .ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.SendError, result.ExitCode);
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

        Diagnostics.Arrange("url, authorization on retry", "http://example.com/, Basic dTpw");
        Diagnostics.Arrange("response frames", "SETTINGS header table 0, HEADERS 401 on 1, stray HEADERS on 1, HEADERS 200 on 3, DATA ok");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator)
            .ExecuteAsync(Http2Context("http://example.com/", output));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("body", "ok", Latin1(output.ToArray()));
        Assert.AreEqual("ok", Latin1(output.ToArray()));
        Diagnostics.Assert("connection count", 1, result.Report!.ConnectionCount);
        Assert.AreEqual(1, result.Report!.ConnectionCount);
        Http2Frame[] headers = [.. (await FramesAfterPreface(connection.Written)).Where(frame => frame.Type == Http2FrameType.Headers)];
        Diagnostics.Act("headers frames sent", headers.Length);
        Diagnostics.Assert("retry stream id", 3, headers[1].StreamId);
        Assert.AreEqual(3, headers[1].StreamId);
        Diagnostics.Assert("retry block first byte", 0x20, headers[1].Payload.Span[0]);
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
        Diagnostics.Arrange("url, authorization on retry", "http://example.com/, Basic dTpw");
        Diagnostics.Arrange("connections", "first: GOAWAY then HEADERS 401; second: HEADERS 204");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(refused, 65536), new ScriptedConnection(accepted, 65536)), authenticator)
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("connection count", 2, result.Report!.ConnectionCount);
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

        Diagnostics.Arrange("url, authorization on retry", "http://example.com/, Basic dTpw");
        Diagnostics.Arrange("response frames", "SETTINGS max concurrent streams 0, HEADERS 401 (end stream)");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(response, 65536)), authenticator)
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Diagnostics.Assert("error message", "Error in the HTTP2 framing layer", result.ErrorMessage);
        Assert.AreEqual("Error in the HTTP2 framing layer", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoHttp2TransfersToOneOrigin_ContinueOneSessionOnThePooledConnection()
    {
        // curl --http2-prior-knowledge http://127.0.0.1:18817/a http://127.0.0.1:18817/b (curl 8.18.0,
        // BL-817 Notes): stream 3 with no second preface, its HEADERS indexing what stream 1's
        // added to the HPACK table, and its WINDOW_UPDATEs counted from the acknowledged 65536.
        string firstRequest = "000022010500000001" + "8286" + "418B089D5C0B8170DC0BCF05DF" + "04022F61" + "7A8825B650C3CB85E5C1" + "53032A2F2A";
        string settingsAcknowledgement = "000000040100000000";
        string secondRequest = "000009010500000003" + "8286C004022F62BFBE";
        string streamThreeWindowUpdates = "000004080000000003009F0000" + "000004080000000003009F0000";
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("x-served", "one")]), isEndStream: true, isEndHeaders: true),
            Http2FrameFactory.CreateHeaders(3, server.Encode([new(":status", "200"), new("x-served", "one")]), isEndStream: true, isEndHeaders: true));
        ScriptedConnection wire = new(response, 65536, Convert.FromHexString(Http2Preface + firstRequest + StreamOneWindowUpdates));
        SessionHoldingConnection connection = new(wire);
        HttpProtocolHandler handler = Handler(new QueueConnector(
            ConnectResult.Connected(connection, null),
            ConnectResult.Connected(connection, null, isReused: true)));
        MemoryStream secondHead = new();
        Diagnostics.Arrange("urls", "http://127.0.0.1:18817/a, http://127.0.0.1:18817/b");
        Diagnostics.Arrange("response frames", "HEADERS 200 x-served one on 1, HEADERS 200 x-served one on 3");

        TransferResult first = await handler.ExecuteAsync(Http2Context("http://127.0.0.1:18817/a", new MemoryStream()));
        TransferResult second = await handler.ExecuteAsync(Http2Context("http://127.0.0.1:18817/b", new MemoryStream(), secondHead));
        await connection.CloseAsync();

        Diagnostics.Act("first exit code", $"{first.ExitCode} ({(int)first.ExitCode}), error: {first.ErrorMessage ?? "(none)"}");
        WriteResult(second);
        Diagnostics.Assert("first exit code", CurlExitCode.Ok, first.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, first.ExitCode);
        Diagnostics.Assert("second exit code", CurlExitCode.Ok, second.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
        Diagnostics.Diff("second head", OneLine("HTTP/2 200 \r\nx-served: one\r\n\r\n"), OneLine(Latin1(secondHead.ToArray())));
        Assert.AreEqual("HTTP/2 200 \r\nx-served: one\r\n\r\n", Latin1(secondHead.ToArray()), "the second head decodes against the first's HPACK table");
        Diagnostics.Assert("returned reusable count", 2, connection.ReturnedReusableCount);
        Assert.AreEqual(2, connection.ReturnedReusableCount);
        Diagnostics.Assert("second connection count", 0, second.Report!.ConnectionCount);
        Assert.AreEqual(0, second.Report!.ConnectionCount, "the second transfer reused the connection");
        Diagnostics.Assert(
            "bytes written",
            Convert.ToHexString(Convert.FromHexString(Http2Preface + firstRequest + StreamOneWindowUpdates + settingsAcknowledgement + secondRequest + streamThreeWindowUpdates + ClosingGoAway)),
            Convert.ToHexString(wire.Written));
        CollectionAssert.AreEqual(
            Convert.FromHexString(Http2Preface + firstRequest + StreamOneWindowUpdates + settingsAcknowledgement + secondRequest + streamThreeWindowUpdates + ClosingGoAway),
            wire.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2ConnectionWhosePeerSentGoAway_IsClosedNotPooled()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateGoAway(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty),
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "0")]), isEndStream: true, isEndHeaders: true));
        ScriptedConnection wire = new(response, 65536);
        SessionHoldingConnection connection = new(wire);

        Diagnostics.Arrange("url", "http://example.com/");
        Diagnostics.Arrange("response frames", "GOAWAY last stream 1 NO_ERROR, HEADERS 200 length 0 (end stream)");

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("returned reusable count", 0, connection.ReturnedReusableCount);
        Assert.AreEqual(0, connection.ReturnedReusableCount);
        Diagnostics.Assert("wire disposed", true, wire.IsDisposed);
        Assert.IsTrue(wire.IsDisposed);
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
