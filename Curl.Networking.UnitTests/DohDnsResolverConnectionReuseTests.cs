using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Http2;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="DohDnsResolver" /> reuses its DoH connections (ADR-1441), with the connection
/// numbers, stream numbers and <c>-v</c> lines curl 8.18.0 (nghttp2, on Windows and on Ubuntu) was
/// measured to use against nginx. The connection is scripted, so no test touches the network.
/// </summary>
[TestClass]
public sealed class DohDnsResolverConnectionReuseTests
{
    private const byte DataFrame = 0x0;
    private const byte HeadersFrame = 0x1;
    private const byte SettingsFrame = 0x4;
    private const byte EndStream = 0x1;
    private const byte EndHeaders = 0x4;

    private const string Reusing = "* Reusing existing https: connection with host 127.0.0.1";

    private static readonly Uri DohUrl = new("https://127.0.0.1:28443/dns-query");

    private static readonly IPAddress Address = IPAddress.Parse("192.0.2.20");

    private static readonly IPAddress Address6 = IPAddress.Parse("2001:db8::20");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ResolveAsync_OfTwoNamesOverH2_SendsTheSecondNameOnStreamsFiveAndSevenOfTheFirstConnection()
    {
        var connector = new TurnTakingConnector("h2", [[.. Settings(), .. Answered(1, 3)], Answered(5, 7)]);
        var events = new RecordingTransferEvents();
        var connectionIdsTaken = 0;
        await using var resolver = new DohDnsResolver(connector, DohUrl) { SubTransferEvents = events, TakeConnectionId = () => ++connectionIdsTaken };
        await resolver.ResolveAsync("a.test", CancellationToken.None);
        var firstNameLines = events.Transcript.Count;

        var addresses = (await resolver.ResolveAsync("b.test", CancellationToken.None)).ToArray();

        var lines = ConnectionLines(events.Transcript.Skip(firstNameLines));
        Diagnostics.Act("second name's lines", string.Join('\n', lines));
        CollectionAssert.AreEqual(new[] { Address6, Address }, addresses);
        Assert.HasCount(1, connector.Opened);
        Assert.AreEqual(1, connectionIdsTaken);
        CollectionAssert.AreEqual(
            new[]
            {
                Reusing,
                "* [HTTP/2] [5] OPENED stream for https://127.0.0.1:28443/dns-query",
                "* Multiplexed connection found",
                Reusing,
                "* [HTTP/2] [7] OPENED stream for https://127.0.0.1:28443/dns-query",
                "* DoH request complete, 1 to go",
                "* Connection #1 to host 127.0.0.1:28443 left intact",
                "* DoH request complete, 0 to go",
            },
            lines);
        var sent = connector.Opened[0].Written.ToArray();
        Assert.AreEqual(1, Occurrences(sent, Http2Connection.ClientPreface.ToArray()));
    }

    [TestMethod]
    public async Task ResolveAsync_OfTwoNamesAgainstAnHttp11Server_ReusesTheConnectionAndOpensASecondForTheSecondNamesSecondQuery()
    {
        // The first connection answers the first name's AAAA and A queries, then the second name's AAAA.
        var connector = Connector("http/1.1", [Ok(DnsRecordType.Aaaa), Ok(DnsRecordType.A), Ok(DnsRecordType.Aaaa)], [Ok(DnsRecordType.A)]);
        var events = new RecordingTransferEvents();
        var nextConnectionId = 1;
        await using var resolver = new DohDnsResolver(connector, DohUrl) { SubTransferEvents = events, TakeConnectionId = () => nextConnectionId++ };

        var first = (await resolver.ResolveAsync("a.test", CancellationToken.None)).ToArray();
        var firstNameLines = events.Transcript.Count;
        nextConnectionId = 3; // The first URL's transfer is done; the second URL's takes 2.
        var second = (await resolver.ResolveAsync("b.test", CancellationToken.None)).ToArray();

        var transcript = events.Transcript.ToArray();
        Diagnostics.Act("transcript", string.Join('\n', transcript));
        CollectionAssert.AreEqual(new[] { Address6, Address }, first);
        CollectionAssert.AreEqual(new[] { Address6, Address }, second);
        Assert.HasCount(2, connector.Opened);
        CollectionAssert.AreEqual(
            new[]
            {
                "* using HTTP/1.x",
                "* Connection #1 to host 127.0.0.1:28443 left intact",
                "* DoH request complete, 1 to go",
                Reusing,
                "* Connection #1 to host 127.0.0.1:28443 left intact",
                "* DoH request complete, 0 to go",
            },
            ConnectionLines(transcript.Take(firstNameLines)));
        CollectionAssert.AreEqual(
            new[]
            {
                Reusing,
                "* Connection #1 to host 127.0.0.1:28443 left intact",
                "* DoH request complete, 1 to go",
                "* using HTTP/1.x",
                "* Connection #3 to host 127.0.0.1:28443 left intact",
                "* DoH request complete, 0 to go",
            },
            ConnectionLines(transcript.Skip(firstNameLines)));
    }

    [TestMethod]
    public async Task ResolveAsync_OfAThirdNameAgainstAnHttp11Server_PutsEachQueryOnAKeptConnectionTheLowestNumberedFirst()
    {
        // Untraced, the first name asks A first.
        var connector = Connector(
            "http/1.1",
            [Ok(DnsRecordType.A), Ok(DnsRecordType.Aaaa), Ok(DnsRecordType.Aaaa), Ok(DnsRecordType.Aaaa)],
            [Ok(DnsRecordType.A), Ok(DnsRecordType.A)]);
        var resolver = new DohDnsResolver(connector, DohUrl);
        await resolver.ResolveAsync("a.test", CancellationToken.None);
        await resolver.ResolveAsync("b.test", CancellationToken.None);

        var addresses = (await resolver.ResolveAsync("c.test", CancellationToken.None)).ToArray();

        Diagnostics.Act("addresses", string.Join(' ', addresses.Select(address => address.ToString())));
        Assert.HasCount(2, connector.Opened);
        CollectionAssert.AreEqual(new[] { Address6, Address }, addresses);
        await resolver.DisposeAsync();
        Assert.IsTrue(connector.Opened.All(connection => connection.IsDisposed));
    }

    [TestMethod]
    public async Task ResolveAsync_OfALaterNameOverPlainHttp_ReusesBothConnections()
    {
        // The later name's AAAA query takes the first connection and its A query the second.
        var connector = Connector(null, [Ok(DnsRecordType.A), Ok(DnsRecordType.Aaaa)], [Ok(DnsRecordType.Aaaa), Ok(DnsRecordType.A)]);
        await using var resolver = new DohDnsResolver(connector, new Uri("http://dns.example/dns-query"));
        await resolver.ResolveAsync("a.test", CancellationToken.None);

        var addresses = (await resolver.ResolveAsync("b.test", CancellationToken.None)).ToArray();

        CollectionAssert.AreEqual(new[] { Address6, Address }, addresses);
        Assert.HasCount(2, connector.Opened);
    }

    [TestMethod]
    public async Task ResolveAsync_OfOneFamilyAfterAnHttp2Name_SendsItOnTheNextStream()
    {
        var connector = Connector("h2", [[.. Settings(), .. Answer(1, DnsRecordType.A)], Answer(3, DnsRecordType.A)]);
        var events = new RecordingTransferEvents();
        await using var resolver = new DohDnsResolver(connector, DohUrl) { SubTransferEvents = events, AddressFamily = AddressFamily.InterNetwork };
        await resolver.ResolveAsync("a.test", CancellationToken.None);

        var addresses = (await resolver.ResolveAsync("b.test", CancellationToken.None)).ToArray();

        CollectionAssert.AreEqual(new[] { Address }, addresses);
        Assert.HasCount(1, connector.Opened);
        StringAssert.Contains(string.Join('\n', events.Transcript), "* [HTTP/2] [3] OPENED stream");
    }

    [TestMethod]
    public async Task ResolveAsync_AfterAnHttpsRecordQueryOverH2_SendsBothAddressQueriesOnItsConnection()
    {
        var connector = Connector("h2", [[.. Settings(), .. Answer(1, DnsRecordType.A)], Answered(3, 5)]);
        await using var resolver = new DohDnsResolver(connector, DohUrl);

        var addresses = (await resolver.ResolveHttpsOriginAsync("a.test", 443, CancellationToken.None)).ToArray();

        CollectionAssert.AreEqual(new[] { Address6, Address }, addresses);
        Assert.HasCount(1, connector.Opened);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenTheKeptH2ConnectionHasClosed_AsksBothQueriesAgainOnANewOne()
    {
        byte[][] answers = [[.. Settings(), .. Answered(1, 3)]];
        var connector = Connector("h2", answers, answers);
        await using var resolver = new DohDnsResolver(connector, DohUrl);
        await resolver.ResolveAsync("a.test", CancellationToken.None);

        var addresses = (await resolver.ResolveAsync("b.test", CancellationToken.None)).ToArray();

        CollectionAssert.AreEqual(new[] { Address6, Address }, addresses);
        Assert.HasCount(2, connector.Opened);
        Assert.IsTrue(connector.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenTheKeptHttp11ConnectionHasClosed_AsksItsQueryAgainOnANewOne()
    {
        // The second name's A query opens a connection; its AAAA query finds the kept one closed.
        var connector = Connector("http/1.1", [Ok(DnsRecordType.A), Ok(DnsRecordType.Aaaa)], [Ok(DnsRecordType.A)], [Ok(DnsRecordType.Aaaa)]);
        await using var resolver = new DohDnsResolver(connector, DohUrl);
        await resolver.ResolveAsync("a.test", CancellationToken.None);

        var addresses = (await resolver.ResolveAsync("b.test", CancellationToken.None)).ToArray();

        CollectionAssert.AreEqual(new[] { Address6, Address }, addresses);
        Assert.HasCount(3, connector.Opened);
        Assert.IsTrue(connector.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenAKeptH2ConnectionAnswersOnlyOneQuery_FailsTheOtherAndClosesIt()
    {
        var connector = Connector("h2", [[.. Settings(), .. Answered(1, 3)], Answer(5, DnsRecordType.A)]);
        await using var resolver = new DohDnsResolver(connector, DohUrl);
        await resolver.ResolveAsync("a.test", CancellationToken.None);

        var addresses = (await resolver.ResolveAsync("b.test", CancellationToken.None)).ToArray();

        CollectionAssert.AreEqual(new[] { Address }, addresses);
        Assert.HasCount(1, connector.Opened);
        Assert.IsTrue(connector.Opened[0].IsDisposed);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n", true)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n", true)]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: TE\r\nContent-Length: 0\r\n", true)]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: Close\r\nContent-Length: 0\r\n", false)]
    [DataRow("HTTP/1.0 200 OK\r\nContent-Length: 0\r\n", false)]
    [DataRow("HTTP/1.0 200 OK\r\nConnection: Keep-Alive\r\nContent-Length: 0\r\n", true)]
    [DataRow("HTTP/1.1 200 OK\r\n", false)]
    public async Task ResolveAsync_ByTheResponseHead_KeepsTheConnectionOnlyWhenItStaysOpen(string head, bool isKept)
    {
        var body = head.Contains("chunked", StringComparison.Ordinal) ? "0\r\n\r\n" : string.Empty;
        var connector = Connector("http/1.1", [Encoding.Latin1.GetBytes($"{head}\r\n{body}")]);
        await using var resolver = new DohDnsResolver(connector, DohUrl) { AddressFamily = AddressFamily.InterNetwork };

        await resolver.ResolveAsync("a.test", CancellationToken.None);

        Assert.AreEqual(!isKept, connector.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenAnExchangeThrows_ClosesItsConnection()
    {
        var connector = new FakeConnector { ReadException = new NotSupportedException() };
        await using var resolver = new DohDnsResolver(connector, DohUrl) { AddressFamily = AddressFamily.InterNetwork };

        await Assert.ThrowsExactlyAsync<NotSupportedException>(async () => await resolver.ResolveAsync("a.test", CancellationToken.None));

        Assert.IsTrue(connector.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_WhileAQueryHoldsAConnection_ClosesItAsTheQueryEnds()
    {
        var connector = Connector("http/1.1", [Ok(DnsRecordType.A)]);
        DohDnsResolver resolver = null!;
        resolver = new DohDnsResolver(connector, DohUrl)
        {
            AddressFamily = AddressFamily.InterNetwork,
            TakeConnectionId = () =>
            {
                Assert.IsTrue(resolver.DisposeAsync().IsCompletedSuccessfully);
                return 0;
            },
        };

        await resolver.ResolveAsync("a.test", CancellationToken.None);

        Assert.IsTrue(connector.Opened[0].IsDisposed);
    }

    // The -v lines that say which connection and stream a query takes, and how it ends.
    private static string[] ConnectionLines(IEnumerable<string> transcript) =>
    [
        .. transcript.Where(line => line.StartsWith("* using ", StringComparison.Ordinal)
            || line.StartsWith("* Multiplexed", StringComparison.Ordinal)
            || line.StartsWith("* Reusing", StringComparison.Ordinal)
            || line.Contains("OPENED stream", StringComparison.Ordinal)
            || (line.StartsWith("* Connection #", StringComparison.Ordinal) && line.EndsWith("left intact", StringComparison.Ordinal))
            || line.StartsWith("* DoH request complete", StringComparison.Ordinal))
            .Where(line => line != "* using HTTP/2"),
    ];

    private static int Occurrences(byte[] bytes, byte[] sought) =>
        Enumerable.Range(0, bytes.Length - sought.Length + 1).Count(start => bytes.AsSpan(start, sought.Length).SequenceEqual(sought));

    // A connector whose connections, in the order opened, each answer one read per given response and then close.
    private static TurnTakingConnector Connector(string? protocol, params byte[][][] connections) =>
        new(protocol, connections);

    // Both address queries' answers, each stream's on its own, AAAA's on the second.
    private static byte[] Answered(int streamOfA, int streamOfAaaa) =>
        [.. Answer(streamOfA, DnsRecordType.A), .. Answer(streamOfAaaa, DnsRecordType.Aaaa)];

    private static byte[] Answer(int streamId, DnsRecordType recordType) =>
    [
        .. Frame(HeadersFrame, EndHeaders, streamId, new HpackEncoder().Encode([new(":status", "200")]).ToArray()),
        .. Frame(DataFrame, EndStream, streamId, Message(recordType)),
    ];

    private static byte[] Settings() => Frame(SettingsFrame, 0, 0, []);

    private static byte[] Frame(byte type, int flags, int streamId, byte[] payload) =>
        [(byte)(payload.Length >> 16), (byte)(payload.Length >> 8), (byte)payload.Length, type, (byte)flags, 0, 0, (byte)(streamId >> 8), (byte)streamId, .. payload];

    private static byte[] Ok(DnsRecordType recordType) =>
        [.. Encoding.Latin1.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {Message(recordType).Length}\r\n\r\n"), .. Message(recordType)];

    // A DNS answer of one record of the type, for the root name.
    private static byte[] Message(DnsRecordType recordType)
    {
        var data = (recordType == DnsRecordType.A ? Address : Address6).GetAddressBytes();
        return [0, 0, 0x81, 0x80, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, (byte)recordType, 0, 1, 0, 0, 0, 60, 0, (byte)data.Length, .. data];
    }

}
