using System.Net;
using System.Net.Sockets;

using Curl.Http2;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DohDnsResolver" /> over HTTP/2 (ADR-1424, fitness finding STD-221): a DoH
/// server whose handshake picks <c>h2</c> gets the query as an HTTP/2 POST, and its answer is read
/// from the stream's HEADERS and DATA frames. The server's frames are scripted, so no test touches
/// the network.
/// </summary>
[TestClass]
public sealed class DohDnsResolverHttp2Tests
{
    private const byte DataFrame = 0x0;
    private const byte HeadersFrame = 0x1;
    private const byte ResetStreamFrame = 0x3;
    private const byte SettingsFrame = 0x4;
    private const byte GoAwayFrame = 0x7;
    private const byte EndStream = 0x1;
    private const byte EndHeaders = 0x4;

    private static readonly Uri DohUrl = new("https://127.0.0.1:29953/dns-query");

    private static readonly IPAddress Address = IPAddress.Parse("192.0.2.20");

    private static readonly IPAddress Address6 = IPAddress.Parse("2001:db8::20");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ResolveAsync_WhenTheServerPicksH2_ReturnsTheAnsweredAddress()
    {
        var connector = Http2Server([.. Settings(), .. Headers("200", EndHeaders), .. Frame(DataFrame, EndStream, 1, Answer())]);

        var addresses = await Resolve(connector, DohUrl);

        CollectionAssert.AreEqual(new[] { Address }, addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenTheServerPicksH2_SendsThePostOnStreamOne()
    {
        var connector = Http2Server([.. Settings(), .. Headers("200", EndHeaders), .. Frame(DataFrame, EndStream, 1, Answer())]);

        await Resolve(connector, new Uri("https://user:pw@dns.example/dns-query?ct=1"));

        var sent = connector.Opened[0].Written.ToArray();
        var query = DnsQueryEncoder.Encode("example.test", DnsRecordType.A).Bytes;
        var preface = Http2Connection.ClientPreface.ToArray();
        CollectionAssert.AreEqual(preface, sent[..preface.Length]);
        var frames = SentFrames(sent[preface.Length..]);
        var fields = new HpackDecoder().Decode(frames.Single(frame => frame.Type == HeadersFrame).Payload)
            .Select(field => $"{field.Name}: {field.Value}")
            .ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                ":method: POST",
                ":scheme: https",
                ":authority: dns.example",
                ":path: /dns-query?ct=1",
                "authorization: Basic dXNlcjpwdw==",
                "accept: */*",
                "content-type: application/dns-message",
                $"content-length: {query.Length}",
            },
            fields);
        var data = frames.Single(frame => frame.Type == DataFrame);
        Assert.AreEqual(EndStream, data.Flags);
        Assert.AreEqual(1, data.StreamId);
        CollectionAssert.AreEqual(query, data.Payload);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenTheServerPicksH2_WritesTheSubTransfersVerboseLines()
    {
        var connector = Http2Server([.. Settings(), .. Headers("200", EndHeaders), .. Frame(DataFrame, EndStream, 1, Answer())]);
        var events = new RecordingTransferEvents();

        await Resolve(connector, DohUrl, events);

        var transcript = string.Join('\n', events.Transcript);
        Diagnostics.Act("transcript", transcript);
        StringAssert.Contains(transcript, "* using HTTP/2\n* [HTTP/2] [1] OPENED stream for https://127.0.0.1:29953/dns-query\n* [HTTP/2] [1] [:method: POST]\n");
        StringAssert.Contains(transcript, "> POST /dns-query HTTP/2");
        StringAssert.Contains(transcript, "< HTTP/2 200 ");
        StringAssert.Contains(transcript, "< content-type: application/dns-message");
        StringAssert.Contains(transcript, "* Connection #1 to host 127.0.0.1:29953 left intact");
    }

    [TestMethod]
    public async Task ResolveAsync_AfterAnInterimHeaderBlockAndBeforeTrailers_ReturnsTheAnsweredAddress()
    {
        var trailers = new HpackEncoder().Encode([new("x-trailer", "1")]).ToArray();
        var connector = Http2Server(
        [
            .. Settings(),
            .. Headers("103", EndHeaders),
            .. Headers("200", EndHeaders),
            .. Frame(DataFrame, 0, 1, Answer()),
            .. Frame(HeadersFrame, EndHeaders | EndStream, 1, trailers),
        ]);

        var addresses = await Resolve(connector, DohUrl);

        CollectionAssert.AreEqual(new[] { Address }, addresses);
    }

    [TestMethod]
    [DataRow("500")]
    [DataRow("abc")]
    public async Task ResolveAsync_WithNoSuccessStatus_ReturnsNoAddress(string status)
    {
        var connector = Http2Server([.. Settings(), .. Headers(status, EndHeaders), .. Frame(DataFrame, EndStream, 1, Answer())]);

        var addresses = await Resolve(connector, DohUrl);

        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_WithAStatusAndNoBody_ReturnsNoAddress()
    {
        var connector = Http2Server([.. Settings(), .. Headers("200", EndHeaders | EndStream)]);

        var addresses = await Resolve(connector, DohUrl);

        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_WithABodyPastCurlsBuffer_ReturnsNoAddress()
    {
        var connector = Http2Server([.. Settings(), .. Headers("200", EndHeaders), .. Frame(DataFrame, 0, 1, new byte[3001])]);

        var addresses = await Resolve(connector, DohUrl);

        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    [DataRow("closed before any frame")]
    [DataRow("closed inside a frame")]
    [DataRow("stream reset")]
    [DataRow("GOAWAY with an error")]
    [DataRow("protocol error")]
    [DataRow("undecodable header block")]
    public async Task ResolveAsync_WhenTheHttp2ExchangeFails_ReturnsNoAddress(string failure)
    {
        byte[] served = failure switch
        {
            "closed before any frame" => [],
            "closed inside a frame" => [.. Settings(), 0, 0],
            "stream reset" => [.. Settings(), .. Frame(ResetStreamFrame, 0, 1, [0, 0, 0, 2])],
            "GOAWAY with an error" => [.. Settings(), .. Frame(GoAwayFrame, 0, 0, [0, 0, 0, 0, 0, 0, 0, 2])],
            "protocol error" => [.. Settings(), .. Frame(DataFrame, 0, 0, [1])],
            _ => [.. Settings(), .. Frame(HeadersFrame, EndHeaders, 1, [0x80])],
        };

        var addresses = await Resolve(Http2Server(served), DohUrl);

        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenTheConnectionThrowsWhatNoHttp2ExchangeFailureIs_LetsItEscape()
    {
        var connector = new FakeConnector { ApplicationProtocol = "h2", ReadException = new NotSupportedException() };

        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => Resolve(connector, DohUrl));
    }

    [TestMethod]
    public async Task ResolveAsync_OfAPlainHttpDohUrl_OffersNoProtocols()
    {
        var connector = new FakeConnector();

        await Resolve(connector, new Uri("http://dns.example/dns-query"));

        Assert.IsNull(connector.Targets[0].ApplicationProtocols);
    }

    [TestMethod]
    public async Task ResolveAsync_OfBothQueriesOverH2_SendsThemAsStreamsOneAndThreeOfOneConnection()
    {
        var connector = Http2Server(BothStreamsAnswered());
        var connectionIdsTaken = 0;
        var resolver = new DohDnsResolver(connector, DohUrl) { TakeConnectionId = () => ++connectionIdsTaken };

        var addresses = (await resolver.ResolveAsync("example.test", CancellationToken.None)).ToArray();

        CollectionAssert.AreEqual(new[] { Address6, Address }, addresses);
        Assert.HasCount(1, connector.Opened);
        Assert.AreEqual(1, connectionIdsTaken);
        Assert.IsFalse(connector.Opened[0].IsDisposed);
        await resolver.DisposeAsync();
        Assert.IsTrue(connector.Opened[0].IsDisposed);
        var frames = SentFrames(connector.Opened[0].Written.ToArray()[Http2Connection.ClientPreface.Length..]);
        var decoder = new HpackDecoder();
        var headers = frames.Where(frame => frame.Type == HeadersFrame).ToArray();
        CollectionAssert.AreEqual(new[] { 1, 3 }, headers.Select(frame => frame.StreamId).ToArray());
        foreach (var frame in headers)
        {
            Assert.Contains(field => field.Name == ":path" && field.Value == "/dns-query", decoder.Decode(frame.Payload));
        }

        var data = frames.Where(frame => frame.Type == DataFrame).ToArray();
        CollectionAssert.AreEqual(new[] { 1, 3 }, data.Select(frame => frame.StreamId).ToArray());
        CollectionAssert.AreEqual(Query(DnsRecordType.A), data[0].Payload);
        CollectionAssert.AreEqual(Query(DnsRecordType.Aaaa), data[1].Payload);
    }

    [TestMethod]
    public async Task ResolveAsync_OfBothQueriesOverH2Traced_WritesCurlsMultiplexingLines()
    {
        var events = new RecordingTransferEvents();
        var resolver = new DohDnsResolver(new TryingConnector(Http2Server(BothStreamsAnswered())), DohUrl) { SubTransferEvents = events };

        await resolver.ResolveAsync("example.test", CancellationToken.None);

        Diagnostics.Act("transcript", string.Join('\n', events.Transcript));
        string[] expected =
        [
            "*   Trying 127.0.0.1:29953...",
            "* Connection #1 is not open enough, cannot reuse",
            "* Found pending candidate for reuse and CURLOPT_PIPEWAIT is set",
            "* Waiting on connection to negotiate possible multiplexing.",
            "* using HTTP/2",
            .. RequestLines(1, Query(DnsRecordType.A).Length),
            "* Multiplexed connection found",
            "* Reusing existing https: connection with host 127.0.0.1",
            .. RequestLines(3, Query(DnsRecordType.Aaaa).Length),
            "< HTTP/2 200 ",
            "< content-type: application/dns-message",
            "< ",
            "< HTTP/2 200 ",
            "< content-type: application/dns-message",
            "< ",
            "* DoH request complete, 1 to go",
            "* Connection #1 to host 127.0.0.1:29953 left intact",
            "* DoH request complete, 0 to go",
        ];
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    [DataRow("stream reset")]
    [DataRow("body past curl's buffer")]
    [DataRow("body past curl's buffer, then reset")]
    public async Task ResolveAsync_WhenOneOfTheTwoStreamsFails_ReturnsTheOthersAddress(string failure)
    {
        byte[] reset = Frame(ResetStreamFrame, 0, 1, [0, 0, 0, 2]);
        byte[] tooLong = [.. Headers("200", EndHeaders), .. Frame(DataFrame, 0, 1, new byte[3001])];
        byte[] first = failure switch
        {
            "stream reset" => reset,
            "body past curl's buffer" => [.. tooLong, .. Frame(DataFrame, EndStream, 1, [0])],
            _ => [.. tooLong, .. reset],
        };
        var connector = Http2Server([.. Settings(), .. first, .. Headers("200", EndHeaders, 3), .. Frame(DataFrame, EndStream, 3, Answer6())]);

        var addresses = (await new DohDnsResolver(connector, DohUrl).ResolveAsync("example.test", CancellationToken.None)).ToArray();

        CollectionAssert.AreEqual(new[] { Address6 }, addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenTheSharedConnectionClosesBeforeTheSecondAnswer_ReturnsTheFirstAndDoesNotSayItIsLeftIntact()
    {
        var connector = Http2Server([.. Settings(), .. Headers("200", EndHeaders), .. Frame(DataFrame, EndStream, 1, Answer())]);
        var events = new RecordingTransferEvents();
        var resolver = new DohDnsResolver(connector, DohUrl) { SubTransferEvents = events };

        var addresses = (await resolver.ResolveAsync("example.test", CancellationToken.None)).ToArray();

        CollectionAssert.AreEqual(new[] { Address }, addresses);
        Assert.DoesNotContain(line => line.EndsWith("left intact", StringComparison.Ordinal), events.Info);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ResolveAsync_OfBothQueriesTracedWhenTheServerPicksNoH2_OpensAConnectionForEach(bool throughProxy)
    {
        var connector = new FakeConnector();
        var events = new RecordingTransferEvents();
        var connectionIdsTaken = 0;
        var resolver = new DohDnsResolver(new TryingConnector(connector), DohUrl)
        {
            SubTransferEvents = events,
            TakeConnectionId = () => ++connectionIdsTaken,
            Proxy = throughProxy ? new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null) : null,
        };

        await resolver.ResolveAsync("example.test", CancellationToken.None);

        Assert.HasCount(2, connector.Opened);
        Assert.AreEqual(2, connectionIdsTaken);
        Assert.AreEqual(throughProxy ? 0 : 1, events.Info.Count(line => line.StartsWith("Waiting on connection", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void DohPipeWaitTraceEvents_GivenEveryEventAndTwoTryingLines_PassesAllOnAndWritesTheWaitingLinesOnce()
    {
        var inner = new CountingTransferEvents();
        ITransferEvents events = new DohPipeWaitTraceEvents(inner, 1);

        events.ReportInfo("  Trying 127.0.0.1:29953...");
        events.ReportInfo("  Trying [::1]:29953...");
        events.ReportWarning("warning");
        events.ReportConnectionOpened(null!);
        events.ReportConnectionReused(null!);
        events.ReportTlsHandshake(null!);
        events.ReportTlsData([], sent: true);
        events.ReportTlsMessage(null!);
        events.ReportTlsTrust(null!);
        events.ReportCertificateVerifyResult(0, isProxy: false);
        events.ReportTlsEarlyData(1);
        events.ReportRequestHeader([]);
        events.ReportResponseHeader([]);
        events.ReportDataSent([]);
        events.ReportDataReceived([]);

        // Five info lines (two Trying, three waiting) and one of each of the thirteen other events.
        Assert.AreEqual(5, inner.InfoCount);
        Assert.AreEqual(13, inner.OtherCount);
    }

    private static FakeConnector Http2Server(byte[] served)
    {
        var connector = new FakeConnector { ApplicationProtocol = "h2" };
        connector.BytesToRead.Add(served);
        return connector;
    }

    private async Task<IPAddress[]> Resolve(FakeConnector connector, Uri dohUrl, ITransferEvents? events = null)
    {
        var resolver = new DohDnsResolver(connector, dohUrl)
        {
            AddressFamily = AddressFamily.InterNetwork,
            SubTransferEvents = events ?? NoTransferEvents.Instance,
        };
        Diagnostics.Arrange("DoH URL", dohUrl);
        var addresses = (await resolver.ResolveAsync("example.test", CancellationToken.None)).ToArray();
        Diagnostics.Act("addresses", string.Join(", ", (object[])addresses));
        return addresses;
    }

    private static byte[] Answer() =>
        DnsTestReplies.Answer(DnsQueryEncoder.Encode("example.test", DnsRecordType.A).Bytes, [Address]);

    private static byte[] Settings() => Frame(SettingsFrame, 0, 0, []);

    private static byte[] Headers(string status, int flags, int streamId = 1) => Frame(
        HeadersFrame,
        flags,
        streamId,
        new HpackEncoder().Encode([new(":status", status), new("content-type", "application/dns-message")]).ToArray());

    private static byte[] BothStreamsAnswered() =>
    [
        .. Settings(),
        .. Headers("200", EndHeaders),
        .. Headers("200", EndHeaders, 3),
        .. Frame(DataFrame, EndStream, 1, Answer()),
        .. Frame(DataFrame, EndStream, 3, Answer6()),
    ];

    private static string[] RequestLines(int streamId, int queryLength) =>
    [
        $"* [HTTP/2] [{streamId}] OPENED stream for https://127.0.0.1:29953/dns-query",
        $"* [HTTP/2] [{streamId}] [:method: POST]",
        $"* [HTTP/2] [{streamId}] [:scheme: https]",
        $"* [HTTP/2] [{streamId}] [:authority: 127.0.0.1:29953]",
        $"* [HTTP/2] [{streamId}] [:path: /dns-query]",
        $"* [HTTP/2] [{streamId}] [accept: */*]",
        $"* [HTTP/2] [{streamId}] [content-type: application/dns-message]",
        $"* [HTTP/2] [{streamId}] [content-length: {queryLength}]",
        "> POST /dns-query HTTP/2",
        "> Host: 127.0.0.1:29953",
        "> Accept: */*",
        "> Content-Type: application/dns-message",
        $"> Content-Length: {queryLength}",
        "> ",
        $"* upload completely sent off: {queryLength} bytes",
    ];

    private static byte[] Query(DnsRecordType type) => DnsQueryEncoder.Encode("example.test", type).Bytes;

    private static byte[] Answer6() => DnsTestReplies.Answer(Query(DnsRecordType.Aaaa), [Address6]);

    // Reports a Trying line before it opens each connection, as TcpConnector does.
    private sealed class TryingConnector(IConnector inner) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            target.Events.ReportInfo("  Trying 127.0.0.1:29953...");
            return inner.ConnectAsync(target, cancellationToken);
        }
    }

    private sealed class CountingTransferEvents : ITransferEvents
    {
        public int InfoCount { get; private set; }

        public int OtherCount { get; private set; }

        public void ReportInfo(string text) => InfoCount++;

        public void ReportWarning(string text) => OtherCount++;

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => OtherCount++;

        public void ReportConnectionReused(ConnectionReusedEvent reused) => OtherCount++;

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => OtherCount++;

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => OtherCount++;

        public void ReportTlsMessage(TlsMessageEvent message) => OtherCount++;

        public void ReportTlsTrust(TlsTrustEvent trust) => OtherCount++;

        public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => OtherCount++;

        public void ReportTlsEarlyData(long bytes) => OtherCount++;

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => OtherCount++;

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => OtherCount++;

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => OtherCount++;

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => OtherCount++;
    }

    // One RFC 9113 frame: length, type, flags, stream identifier, payload.
    private static byte[] Frame(byte type, int flags, int streamId, byte[] payload) =>
    [
        (byte)(payload.Length >> 16), (byte)(payload.Length >> 8), (byte)payload.Length,
        type, (byte)flags,
        0, 0, 0, (byte)streamId,
        .. payload,
    ];

    private static List<(byte Type, byte Flags, int StreamId, byte[] Payload)> SentFrames(byte[] sent)
    {
        List<(byte, byte, int, byte[])> frames = [];
        var offset = 0;
        while (offset + 9 <= sent.Length)
        {
            var length = (sent[offset] << 16) | (sent[offset + 1] << 8) | sent[offset + 2];
            frames.Add((sent[offset + 3], sent[offset + 4], sent[offset + 8], sent[(offset + 9)..(offset + 9 + length)]));
            offset += 9 + length;
        }

        return frames;
    }
}
