using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http2Server;

[TestClass]
public sealed class Http2ServerSessionTests
{
    private const string Reply1700 = "HTTP/1.1 200 OK\r\nDate: Tue, 09 Nov 2010 14:49:00 GMT\r\nServer: test-server/fake\r\nContent-Length: 6\r\nConnection: close\r\n\r\n-foo-\n";

    private static readonly byte[] Preface = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray();

    private static readonly HeaderField[] Get1700 =
        [new(":method", "GET"), new(":scheme", "http"), new(":authority", "127.0.0.1:8080"), new(":path", "/1700"), new("user-agent", "curl/8.22.0"), new("accept", "*/*")];

    [TestMethod]
    public async Task RunAsync_PriorKnowledgeGet_ExchangesSettingsRelaysTheRequestAndAnswersPingAndGoAway()
    {
        var backend = new RecordingBackendConnector(Reply1700);
        (Stream client, Task session) = Start(backend);
        var encoder = new HpackEncoder();
        await client.WriteAsync(Preface);
        await Write(client, Http2FrameFactory.CreateSettings([new(Http2SettingIdentifier.EnablePush, 0)]));
        await Write(client, Http2FrameFactory.CreateHeaders(1, encoder.Encode(Get1700), isEndStream: true, isEndHeaders: true));

        Http2Frame settings = await Read(client);
        Http2Frame acknowledgement = await Read(client);
        Http2Frame headers = await Read(client);
        Http2Frame data = await Read(client);
        await Write(client, Http2FrameFactory.CreateSettingsAcknowledgement());
        await Write(client, Http2FrameFactory.CreatePing(7, isAcknowledgement: true));
        await Write(client, Http2FrameFactory.CreatePing(9, isAcknowledgement: false));
        Http2Frame ping = await Read(client);
        await Write(client, Http2FrameFactory.CreateGoAway(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty));
        Http2Frame goAway = await Read(client);
        await session;

        Assert.AreEqual(new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 100), Http2FramePayloadParser.ParseSettings(settings).Single());
        Assert.IsTrue(acknowledgement.HasFlag(Http2FrameFlags.Acknowledgement));
        Assert.AreEqual(
            ":status: 200|date: Tue, 09 Nov 2010 14:49:00 GMT|content-length: 6|server: nghttpx|via: 1.1 nghttpx",
            Decode(new HpackDecoder(), headers));
        Assert.AreEqual((Http2FrameType.Data, 1, "-foo-\n"), (data.Type, data.StreamId, Encoding.Latin1.GetString(data.Payload.Span)));
        Assert.IsTrue(data.HasFlag(Http2FrameFlags.EndStream));
        Assert.AreEqual((Http2FrameType.Ping, 9UL), (ping.Type, Http2FramePayloadParser.ParsePing(ping)));
        Assert.AreEqual(1, Http2FramePayloadParser.ParseGoAway(goAway).LastStreamId);
        Assert.AreEqual(
            "GET /1700 HTTP/1.1\r\nHost: 127.0.0.1:8080\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\nX-Forwarded-Proto: http\r\nVia: 2 nghttpx\r\n\r\n",
            backend.Requests.Single());
    }

    [TestMethod]
    public async Task RunAsync_UpgradeH2c_Answers101ThenTheReplyOnStream1AndGoAwayWhenTheClientCloses()
    {
        var backend = new RecordingBackendConnector("HTTP/1.1 204 No Content\r\n\r\n");
        (Stream client, Task session) = Start(backend);
        await client.WriteAsync("GET /1700 HTTP/1.1\r\nHost: 127.0.0.1:8080\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\nConnection: Upgrade, HTTP2-Settings\r\nUpgrade: h2c\r\nHTTP2-Settings: AAMAAABkAAQAoAAAAAIAAAAA\r\n\r\n"u8.ToArray());

        byte[] switching = new byte[71];
        await client.ReadExactlyAsync(switching);
        Http2Frame settings = await Read(client);
        await client.WriteAsync(Preface);
        Http2Frame headers = await Read(client);
        await client.DisposeAsync();
        Http2Frame goAway = await Read(client);
        await session;

        Assert.AreEqual("HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n", Encoding.Latin1.GetString(switching));
        Assert.AreEqual(Http2FrameType.Settings, settings.Type);
        Assert.AreEqual((1, true), (headers.StreamId, headers.HasFlag(Http2FrameFlags.EndStream)));
        Assert.AreEqual(":status: 204|server: nghttpx|via: 1.1 nghttpx", Decode(new HpackDecoder(), headers));
        Assert.AreEqual(Http2FrameType.GoAway, goAway.Type);
        Assert.AreEqual(
            "GET /1700 HTTP/1.1\r\nHost: 127.0.0.1:8080\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\nX-Forwarded-Proto: http\r\nVia: 2 nghttpx\r\n\r\n",
            backend.Requests.Single());
    }

    [TestMethod]
    [DataRow("GET / HTTP/1.1\r\nHost: h\r\n\r\n")]
    [DataRow("GET /\r\nUpgrade: h2c\r\n\r\n")]
    [DataRow("")]
    public async Task RunAsync_OpeningIsNeitherThePrefaceNorAnH2cUpgrade_ClosesWithoutAWord(string opening)
    {
        (Stream client, Task session) = Start(new RecordingBackendConnector(Reply1700));
        await client.WriteAsync(Encoding.Latin1.GetBytes(opening));
        if (opening.Length == 0)
        {
            await client.DisposeAsync();
        }

        await session;

        Assert.AreEqual(0, await client.ReadAsync(new byte[1]));
    }

    [TestMethod]
    public async Task RunAsync_PostLargerThanTheDefaultWindow_SendsWindowUpdatesAndRelaysTheWholeBody()
    {
        var backend = new RecordingBackendConnector(Reply1700);
        (Stream client, Task session) = Start(backend);
        var encoder = new HpackEncoder();
        byte[] block = encoder.Encode([new(":method", "POST"), new(":authority", "h"), new(":path", "/p"), new("content-length", "81920")]);
        await client.WriteAsync(Preface);
        await Write(client, Http2FrameFactory.CreateHeaders(1, block.AsMemory(0, 3), isEndStream: false, isEndHeaders: false));
        await Write(client, Http2FrameFactory.CreateContinuation(1, block.AsMemory(3), isEndHeaders: true));
        await Write(client, Http2FrameFactory.CreatePriority(1, new Http2Priority(0, false, 16)));
        for (int i = 0; i < 5; i++)
        {
            await Write(client, Http2FrameFactory.CreateData(1, new byte[16384], isEndStream: false));
        }

        await Write(client, Http2FrameFactory.CreateData(1, ReadOnlyMemory<byte>.Empty, isEndStream: false));
        await Write(client, Http2FrameFactory.CreateHeaders(1, encoder.Encode([new("x-trailer", "1")]), isEndStream: true, isEndHeaders: true));

        var frames = new List<Http2Frame>();
        while (frames.Count == 0 || frames[^1].Type != Http2FrameType.Data)
        {
            frames.Add(await Read(client));
        }

        await client.DisposeAsync();
        await session;

        Assert.AreEqual(81920, frames.Where(f => f.Type == Http2FrameType.WindowUpdate && f.StreamId == 0).Sum(Http2FramePayloadParser.ParseWindowUpdate));
        Assert.AreEqual(81920, frames.Where(f => f.Type == Http2FrameType.WindowUpdate && f.StreamId == 1).Sum(Http2FramePayloadParser.ParseWindowUpdate));
        Assert.IsFalse(frames.Any(f => f.Type == Http2FrameType.PushPromise));
        Assert.AreEqual(
            "POST /p HTTP/1.1\r\nHost: h\r\nContent-Length: 81920\r\nX-Forwarded-Proto: http\r\nVia: 2 nghttpx\r\n\r\n" + new string('\0', 81920),
            backend.Requests.Single());
    }

    [TestMethod]
    public async Task RunAsync_ReplyLargerThanTheClientWindow_NeverSendsDataPastEitherWindow()
    {
        var backend = new RecordingBackendConnector($"HTTP/1.1 200 OK\r\nContent-Length: 100000\r\n\r\n{new string('x', 100000)}");
        (Stream client, Task session) = Start(backend);
        await client.WriteAsync(Preface);
        await Write(client, Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode(Get1700), isEndStream: true, isEndHeaders: true));
        await Read(client);

        int received = await ReadDataUntilAsync(client, Http2Settings.DefaultInitialWindowSize);
        await Write(client, Http2FrameFactory.CreateWindowUpdate(1, 100000));
        await Write(client, Http2FrameFactory.CreateWindowUpdate(99, 5));
        await Write(client, Http2FrameFactory.CreatePing(1, isAcknowledgement: false));
        Http2Frame afterStreamUpdate = await Read(client);
        await Write(client, Http2FrameFactory.CreateWindowUpdate(0, 100000));
        received += await ReadDataUntilAsync(client, 100000 - received);
        await Write(client, Http2FrameFactory.CreateGoAway(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty));
        Http2Frame goAway = await Read(client);
        await session;

        Assert.AreEqual(Http2FrameType.Ping, afterStreamUpdate.Type);
        Assert.AreEqual(100000, received);
        Assert.AreEqual(Http2FrameType.GoAway, goAway.Type);
    }

    [TestMethod]
    public async Task RunAsync_ClientClosesWhileTheReplyWaitsForWindow_SendsGoAwayAndEnds()
    {
        var backend = new RecordingBackendConnector($"HTTP/1.1 200 OK\r\nContent-Length: 70000\r\n\r\n{new string('x', 70000)}");
        (Stream client, Task session) = Start(backend);
        await client.WriteAsync(Preface);
        await Write(client, Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode(Get1700), isEndStream: true, isEndHeaders: true));
        await Read(client);
        await Read(client);

        await ReadDataUntilAsync(client, Http2Settings.DefaultInitialWindowSize);
        await client.DisposeAsync();
        Http2Frame goAway = await Read(client);
        await session;

        Assert.AreEqual(Http2FrameType.GoAway, goAway.Type);
    }

    [TestMethod]
    public void ToUpgradedRequest_UpgradeWithoutHost_HasAnEmptyAuthority()
    {
        IReadOnlyList<HeaderField>? request = Http2ServerSession.ToUpgradedRequest("GET /x HTTP/1.1\r\nUpgrade: h2c\r\nbare\r\n\r\n");

        Assert.AreEqual(":method: GET|:scheme: http|:authority: |:path: /x|upgrade: h2c|: bare", string.Join("|", request!.Select(f => $"{f.Name}: {f.Value}")));
    }

    private static (Stream Client, Task Session) Start(IConnector backend)
    {
        (InMemoryDuplexStream client, InMemoryDuplexStream server) = InMemoryDuplexStream.CreatePair();
        Task session = new Http2ServerSession(server, backend, new ConnectTarget("127.0.0.1", 8080, false)).RunAsync(CancellationToken.None);
        return (client, session);
    }

    // DATA payload bytes on stream 1 until `count` have arrived, skipping other frames.
    private static async Task<int> ReadDataUntilAsync(Stream client, int count)
    {
        int received = 0;
        while (received < count)
        {
            Http2Frame frame = await Read(client);
            received += frame.Type == Http2FrameType.Data ? frame.Payload.Length : 0;
        }

        return received;
    }

    private static Task Write(Stream client, Http2Frame frame) => Http2FrameCodec.WriteAsync(client, frame, CancellationToken.None);

    private static async Task<Http2Frame> Read(Stream client) =>
        await Http2FrameCodec.ReadAsync(client, Http2FrameCodec.LargestMaximumFrameSize, CancellationToken.None) ?? throw new EndOfStreamException();

    private static string Decode(HpackDecoder decoder, Http2Frame headers) =>
        string.Join("|", decoder.Decode(Http2FramePayloadParser.ParseHeaders(headers).Fragment.Span).Select(f => $"{f.Name}: {f.Value}"));
}
