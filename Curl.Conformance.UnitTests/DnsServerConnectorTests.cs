using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins the dnsd emulation against upstream's <c>tests/server/dnsd.c</c> at <c>curl-8_22_0</c>.</summary>
[TestClass]
public sealed class DnsServerConnectorTests
{
    private const string Addresses = "<reply>\n<dns>\nA: 127.0.0.1\nAAAA: [::1]\n</dns>\n</reply>\n";

    private static readonly IPEndPoint DnsEndPoint = new(IPAddress.Loopback, DnsServerConnector.DnsPort);

    // ID 0x1234, flags 0x0100 (recursion desired), one question.
    private static readonly byte[] QueryHeader = [0x12, 0x34, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    // examplehost.example as labels, then the closing zero.
    private static readonly byte[] ExampleName = [11, .. "examplehost"u8, 7, .. "example"u8, 0];

    [TestMethod]
    public async Task OpenAsync_AnotherPort_ReachesTheWrappedConnector()
    {
        DnsServerConnector dns = new(ParsedTestCase.From(Addresses), new UnreachableDatagramConnector());

        DatagramOpenResult result = await dns.OpenAsync("127.0.0.1", 69, CancellationToken.None);

        Assert.AreEqual(UnreachableDatagramConnector.Message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenAsync_HostName_ServesOnLoopback()
    {
        await using IDatagramChannel channel = await OpenAsync(new DnsServerConnector(ParsedTestCase.From(Addresses), new UnreachableDatagramConnector()), "localhost");

        Assert.AreEqual(DnsEndPoint, channel.ServerEndPoint);
    }

    [TestMethod]
    public async Task AQuery_ConfiguredAddress_IsLoggedAndAnsweredAsDnsdAnswers()
    {
        DnsServerConnector dns = new(ParsedTestCase.From(Addresses), new UnreachableDatagramConnector());
        await using IDatagramChannel channel = await OpenAsync(dns, "127.0.0.1");

        (byte[] reply, EndPoint source) = await AskAsync(channel, 1);

        byte[] expected = [0x12, 0x34, 0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, .. ExampleName, 0x00, 0x01, 0x00, 0x01,
            0xc0, 0x0c, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x0a, 0x14, 0x00, 0x04, 127, 0, 0, 1];
        CollectionAssert.AreEqual(expected, reply);
        Assert.AreEqual(DnsEndPoint, source);
        Assert.AreEqual("QNAME examplehost.example QTYPE A\n", dns.QueryLog);
    }

    [TestMethod]
    public async Task AaaaQuery_BracketedAddress_IsAnsweredWithItsSixteenBytes()
    {
        DnsServerConnector dns = new(ParsedTestCase.From(Addresses), new UnreachableDatagramConnector());
        await using IDatagramChannel channel = await OpenAsync(dns, "127.0.0.1");

        (byte[] reply, _) = await AskAsync(channel, 28);

        Assert.AreEqual(1, reply[7]);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x1c, 0x00, 0x01, 0x00, 0x00, 0x0a, 0x14, 0x00, 0x10 }, reply[^26..^16]);
        CollectionAssert.AreEqual(IPAddress.IPv6Loopback.GetAddressBytes(), reply[^16..]);
        Assert.AreEqual("QNAME examplehost.example QTYPE AAAA\n", dns.QueryLog);
    }

    [TestMethod]
    [DataRow("<reply>\n<dns>\nA: none\nAAAA: none\n</dns>\n</reply>\n", 1, "A")]
    [DataRow("<reply>\n<dns>\n</dns>\n</reply>\n", 28, "AAAA")]
    [DataRow(Addresses, 0x41, "HTTPS")]
    [DataRow(Addresses, 15, "<unknown>")]
    public async Task Query_NoAddressForItsType_IsAnsweredWithNoRecord(string sections, int type, string loggedType)
    {
        DnsServerConnector dns = new(ParsedTestCase.From(sections), new UnreachableDatagramConnector());
        await using IDatagramChannel channel = await OpenAsync(dns, "127.0.0.1");

        (byte[] reply, _) = await AskAsync(channel, (byte)type);

        CollectionAssert.AreEqual((byte[])[0x12, 0x34, 0x81, 0x80, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, .. ExampleName, 0x00, (byte)type, 0x00, 0x01], reply);
        Assert.AreEqual($"QNAME examplehost.example QTYPE {loggedType}\n", dns.QueryLog);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x12, 0x34, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 1, 0x61, 0 })]
    [DataRow(new byte[] { 0x12, 0x34, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 40, 0x61, 0x62, 0x63, 0x64 })]
    public async Task Query_TooShortOrNameRunsPastItsEnd_IsNotAnsweredOrLogged(byte[] query)
    {
        DnsServerConnector dns = new(ParsedTestCase.From(Addresses), new UnreachableDatagramConnector());
        await using IDatagramChannel channel = await OpenAsync(dns, "127.0.0.1");
        using CancellationTokenSource cancellation = new();

        await channel.SendAsync(query, channel.ServerEndPoint, CancellationToken.None);
        ValueTask<DatagramReceived> receive = channel.ReceiveAsync(new byte[512], cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await receive);
        Assert.AreEqual(string.Empty, dns.QueryLog);
    }

    [TestMethod]
    public async Task Queries_OnTwoChannels_AreLoggedInOrder()
    {
        DnsServerConnector dns = new(ParsedTestCase.From(Addresses), new UnreachableDatagramConnector());
        await using IDatagramChannel first = await OpenAsync(dns, "127.0.0.1");
        await using IDatagramChannel second = await OpenAsync(dns, "127.0.0.1");

        await AskAsync(first, 1);
        await AskAsync(second, 28);

        Assert.AreEqual("QNAME examplehost.example QTYPE A\nQNAME examplehost.example QTYPE AAAA\n", dns.QueryLog);
    }

    private static async Task<IDatagramChannel> OpenAsync(DnsServerConnector dns, string host) =>
        (await dns.OpenAsync(host, DnsServerConnector.DnsPort, CancellationToken.None)).Channel!;

    private static async Task<(byte[] Reply, EndPoint Source)> AskAsync(IDatagramChannel channel, byte type)
    {
        await channel.SendAsync((byte[])[.. QueryHeader, .. ExampleName, 0x00, type, 0x00, 0x01], channel.ServerEndPoint, CancellationToken.None);
        byte[] buffer = new byte[512];
        DatagramReceived received = await channel.ReceiveAsync(buffer, CancellationToken.None);
        return (buffer[..received.Length], received.RemoteEndPoint);
    }
}
