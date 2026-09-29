using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins the h2c upgrade <c>--http2</c> asks for over cleartext (BL-716) against curl.se's
/// nghttp2 build of curl 8.18.0 through <c>Record-CurlExchange.ps1</c>: the request bytes it
/// sent with <c>-A curl/8.18.0</c>, and what it wrote for a <c>101</c> followed by HTTP/2
/// frames and for a server that ignores the upgrade (BL-716 Notes).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string UpgradeRequest =
        "GET / HTTP/1.1\r\nHost: 127.0.0.1:48717\r\nUser-Agent: curl/8.18.0\r\nAccept: */*\r\n"
        + "Upgrade: h2c\r\nHTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA\r\nConnection: Upgrade, HTTP2-Settings\r\n\r\n";

    private const string SwitchingProtocolsHead = "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n";

    /// <summary>The server's empty SETTINGS, then <c>:status 200</c> and <c>hi\n</c> on stream 1, as the recording sent them.</summary>
    private const string UpgradedResponseFrames =
        "000000040000000000" + "00000101040000000188" + "000003000100000001" + "68690a";

    /// <summary>The acknowledgement of the server's SETTINGS curl sent after its preface.</summary>
    private const string SettingsAcknowledgement = "000000040100000000";

    [TestMethod]
    public async Task ExecuteAsync_Http2UpgradeAnswered101_SendsThePrefaceAndReadsTheResponseFromStream1()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            byte[] response = [.. Encoding.Latin1.GetBytes(SwitchingProtocolsHead), .. Convert.FromHexString(UpgradedResponseFrames)];
            ScriptedConnection connection = new(response, chunkSize);
            MemoryStream output = new();
            MemoryStream headerOutput = new();
            RecordingTransferEvents events = new();

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(UpgradeContext("http://127.0.0.1:48717/", output, headerOutput, events));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(SwitchingProtocolsHead + "HTTP/2 200 \r\n\r\n", Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("hi\n", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(200, result.Report!.ResponseCode);
            Assert.AreEqual(new Version(2, 0), result.Report.HttpVersion);
            StringAssert.StartsWith(
                Convert.ToHexString(connection.Written),
                Convert.ToHexString(Encoding.Latin1.GetBytes(UpgradeRequest)) + Http2Preface.ToUpperInvariant() + SettingsAcknowledgement,
                $"Chunk size {chunkSize}");
            Assert.IsTrue(connection.IsMarkedReusable, $"Chunk size {chunkSize}: the upgraded connection keeps its HTTP/2 session for later requests");
            CollectionAssert.Contains(events.Info, HttpConnectionInfoLines.SwitchingToHttp2);
            Assert.AreEqual("Connection #0 to host 127.0.0.1:48717 left intact", events.Info[^1], $"Chunk size {chunkSize}");
            StringAssert.EndsWith(Convert.ToHexString(connection.Written), ClosingGoAway, $"Chunk size {chunkSize}: a connection that holds no session is shut down with curl's GOAWAY");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2UpgradeAnswered101WithFramesInTheSameRead_ReportsTheBytesCopied()
    {
        byte[] response = [.. Encoding.Latin1.GetBytes(SwitchingProtocolsHead), .. Convert.FromHexString(UpgradedResponseFrames)];
        RecordingTransferEvents events = new();

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(UpgradeContext("http://127.0.0.1:48717/", new MemoryStream(), null, events));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        int switching = events.Info.IndexOf(HttpConnectionInfoLines.SwitchingToHttp2);
        Assert.AreEqual("Copied HTTP/2 data in stream buffer to connection buffer after upgrade: len=31", events.Info[switching + 1]);
        int http2Head = events.Events.IndexOf("< HTTP/2 200 \r\n");
        Assert.IsGreaterThan(events.Events.IndexOf("* " + HttpConnectionInfoLines.SwitchingToHttp2), http2Head);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2UpgradeIgnored_ReadsTheHttp11ResponseAndKeepsTheConnection()
    {
        // curl --http2 -v http://127.0.0.1:48716/ against a server answering 200 over HTTP/1.1.
        const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nhi\n";
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(Response, chunkSize, UpgradeRequest.Replace("48717", "48716", StringComparison.Ordinal));
            MemoryStream output = new();
            MemoryStream headerOutput = new();
            RecordingTransferEvents events = new();

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(UpgradeContext("http://127.0.0.1:48716/", output, headerOutput, events));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\n", Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("hi\n", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(new Version(1, 1), result.Report!.HttpVersion);
            Assert.IsTrue(connection.IsMarkedReusable, $"Chunk size {chunkSize}: curl leaves the connection intact");
            CollectionAssert.DoesNotContain(events.Info, HttpConnectionInfoLines.SwitchingToHttp2);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2OverTls_SendsNoUpgrade()
    {
        const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";
        const string Request = "GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.18.0\r\nAccept: */*\r\n\r\n";
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(Response), 65536, Encoding.Latin1.GetBytes(Request)) { IsSecure = true };
        MemoryStream output = new();

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(UpgradeContext("https://example.com/", output, null, new RecordingTransferEvents()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2UpgradeAnswered101ThenClosed_FailsAsHttp2FramingError()
    {
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(SwitchingProtocolsHead), 65536);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(UpgradeContext("http://127.0.0.1:48717/", new MemoryStream(), null, new RecordingTransferEvents()));

        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2Upgrade401OnStream1_RetriesAsHeadersOnStream3OfTheSameConnection()
    {
        // curl --http2 -v --anyauth -u a:b http://127.0.0.1:48867/a (BL-866 Notes): the 401 read
        // from stream 1 leaves the connection intact and the retry goes out as HEADERS on stream 3.
        // The server allows one stream at a time, so stream 1 must be closed once its response ends.
        HpackEncoder server = new();
        byte[] response =
        [
            .. Encoding.Latin1.GetBytes(SwitchingProtocolsHead),
            .. Http2Response(
                Http2FrameFactory.CreateSettings([new(Http2SettingIdentifier.MaxConcurrentStreams, 1)]),
                Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "401"), new("www-authenticate", "Basic realm=\"x\"")]), isEndStream: false, isEndHeaders: true),
                Http2FrameFactory.CreateData(1, "no\n"u8.ToArray(), isEndStream: true),
                Http2FrameFactory.CreateHeaders(3, server.Encode([new(":status", "200"), new("content-length", "2")]), isEndStream: false, isEndHeaders: true),
                Http2FrameFactory.CreateData(3, "ok"u8.ToArray(), isEndStream: true)),
        ];
        ScriptedConnection connection = new(response, 65536);
        MemoryStream output = new();
        RecordingTransferEvents events = new();

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(null, "Basic YTpi"))
            .ExecuteAsync(UpgradeContext("http://127.0.0.1:48867/a", output, null, events));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
        Assert.AreEqual(1, result.Report!.ConnectionCount);
        Http2Frame retry = (await FramesAfterUpgradeRequest(connection.Written)).Single(frame => frame.Type == Http2FrameType.Headers);
        Assert.AreEqual(3, retry.StreamId);
        Assert.AreEqual("Basic YTpi", new HpackDecoder().Decode(retry.Payload.Span).Single(field => field.Name == "authorization").Value);
        Assert.AreEqual("Connection #0 to host 127.0.0.1:48867 left intact", events.Info[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoTransfersAfterAnHttp2Upgrade_SendTheSecondOnStream3OfThePooledConnection()
    {
        // curl --http2 -v http://127.0.0.1:48866/a http://127.0.0.1:48866/b (BL-866 Notes): the
        // first is left intact and the second reuses its connection as HEADERS on stream 3.
        HpackEncoder server = new();
        byte[] response =
        [
            .. Encoding.Latin1.GetBytes(SwitchingProtocolsHead),
            .. Http2Response(
                Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true),
                Http2FrameFactory.CreateData(1, "hi\n"u8.ToArray(), isEndStream: true),
                Http2FrameFactory.CreateHeaders(3, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true),
                Http2FrameFactory.CreateData(3, "b\n"u8.ToArray(), isEndStream: true)),
        ];
        ScriptedConnection wire = new(response, 65536);
        SessionHoldingConnection connection = new(wire);
        HttpProtocolHandler handler = Handler(new QueueConnector(
            ConnectResult.Connected(connection, null),
            ConnectResult.Connected(connection, null, isReused: true)));
        RecordingTransferEvents firstEvents = new();
        MemoryStream secondOutput = new();

        TransferResult first = await handler.ExecuteAsync(UpgradeContext("http://127.0.0.1:48866/a", new MemoryStream(), null, firstEvents));
        TransferResult second = await handler.ExecuteAsync(UpgradeContext("http://127.0.0.1:48866/b", secondOutput, null, new RecordingTransferEvents()));
        await connection.CloseAsync();

        Assert.AreEqual(CurlExitCode.Ok, first.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
        Assert.AreEqual("Connection #0 to host 127.0.0.1:48866 left intact", firstEvents.Info[^1]);
        Assert.AreEqual("b\n", Latin1(secondOutput.ToArray()));
        Assert.AreEqual(2, connection.ReturnedReusableCount);
        Assert.AreEqual(new Version(2, 0), second.Report!.HttpVersion);
        Http2Frame next = (await FramesAfterUpgradeRequest(wire.Written)).Single(frame => frame.Type == Http2FrameType.Headers);
        Assert.AreEqual(3, next.StreamId);
        Assert.AreEqual(Http2FrameFlags.EndStream | Http2FrameFlags.EndHeaders, next.Flags);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2UpgradeWithTrailersOnStream1_ReadsTheStreamToItsEndAndWritesThem()
    {
        HpackEncoder server = new();
        byte[] response =
        [
            .. Encoding.Latin1.GetBytes(SwitchingProtocolsHead),
            .. Http2Response(
                Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "2")]), isEndStream: false, isEndHeaders: true),
                Http2FrameFactory.CreateData(1, "ok"u8.ToArray(), isEndStream: false),
                Http2FrameFactory.CreateHeaders(1, server.Encode([new("x-checksum", "1")]), isEndStream: true, isEndHeaders: true)),
        ];
        ScriptedConnection connection = new(response, 65536);
        MemoryStream headerOutput = new();

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(UpgradeContext("http://127.0.0.1:48717/", new MemoryStream(), headerOutput, new RecordingTransferEvents()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.EndsWith(Latin1(headerOutput.ToArray()), "HTTP/2 200 \r\ncontent-length: 2\r\n\r\nx-checksum: 1\r\n");
        Assert.IsTrue(connection.IsMarkedReusable);
    }

    /// <summary>Reads the frames the client wrote after its HTTP/1.1 upgrade request and its preface.</summary>
    private static Task<List<Http2Frame>> FramesAfterUpgradeRequest(byte[] written)
    {
        int requestEnd = written.AsSpan().IndexOf("\r\n\r\n"u8) + 4;
        return FramesAfterPreface(written[requestEnd..]);
    }

    private static TransferContext UpgradeContext(string url, Stream output, Stream? headerOutput, ITransferEvents events) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            HeaderOutput = headerOutput,
            Events = events,
            Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2, UserAgent = MeasuredUserAgent },
        };
}
