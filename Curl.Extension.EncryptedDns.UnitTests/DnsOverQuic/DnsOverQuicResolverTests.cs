using System.Buffers.Binary;
using System.Net;
using Curl.Quic;

namespace Curl.Extension.EncryptedDns.DnsOverQuic;

/// <summary>
/// Checks <see cref="DnsOverQuicResolver" /> against RFC 9250 over an in-memory QUIC server on a fake datagram
/// channel: ALPN <c>doq</c> and port 853 (section 4.1.1), one client bidirectional stream per query ended with FIN
/// (section 4.2), Message ID 0 (section 4.2.1), the two-octet length prefix (section 4.2), and closing with
/// DOQ_NO_ERROR or DOQ_PROTOCOL_ERROR (sections 4.3 and 4.5).
/// </summary>
[TestClass]
public sealed class DnsOverQuicResolverTests
{
    private const int HangTimeoutMilliseconds = 120_000;

    private static readonly DnsOverQuicServer Server = new("dns.example");

    private static readonly IPAddress V6 = IPAddress.Parse("2001:db8::1");

    private static readonly IPAddress V4 = IPAddress.Parse("192.0.2.1");

    [TestMethod]
    public void CreateQuicSettings_Rfc9250Section4_1_1_OffersOnlyTheDoqToken()
    {
        QuicClientSettings settings = DnsOverQuicResolver.CreateQuicSettings(new DnsOverQuicServer("dns.example", TlsServerName: "resolver.example"));

        CollectionAssert.AreEqual(new[] { "doq" }, settings.Tls.ApplicationProtocols.ToArray());
        Assert.AreEqual("resolver.example", settings.Tls.ServerName);
    }

    [TestMethod]
    public void DnsOverQuicServer_Rfc9250Section4_1_1_DefaultsToPort853()
    {
        DnsOverQuicServer server = new("dns.example");

        Assert.AreEqual(853, server.Port);
        Assert.AreEqual("dns.example", server.AuthenticationName);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ResolveAsync_Rfc9250Section4_1_1_OpensTheChannelToPort853AndNegotiatesDoq()
    {
        using QuicTestServer server = DoqServer();
        DoqTestChannel channel = new(server) { Answer = Answering };
        FakeDatagramConnector connector = new(channel);

        IReadOnlyList<IPAddress> addresses = await Resolver(connector).ResolveAsync("example.com", CancellationToken.None);

        Assert.AreEqual(("dns.example", 853), connector.Opens.Single());
        Assert.IsTrue(server.ClientFinishedReceived);
        CollectionAssert.AreEqual(new[] { V6, V4 }, addresses.ToArray());
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ResolveAsync_Rfc9250Section4_2_SendsEachQueryOnItsOwnClientBidirectionalStreamEndedWithFin()
    {
        using QuicTestServer server = DoqServer();
        DoqTestChannel channel = new(server) { Answer = Answering };

        await Resolver(new FakeDatagramConnector(channel)).ResolveAsync("example.com", CancellationToken.None);

        CollectionAssert.AreEquivalent(new ulong[] { 0, 4 }, channel.Queries.Select(query => query.StreamId).ToArray());
        foreach ((_, byte[] query) in channel.Queries)
        {
            Assert.AreEqual(query.Length - 2, BinaryPrimitives.ReadUInt16BigEndian(query), "two-octet length prefix");
            Assert.AreEqual(0, BinaryPrimitives.ReadUInt16BigEndian(query.AsSpan(2)), "Message ID 0 (section 4.2.1)");
        }

        CollectionAssert.AreEquivalent(new ushort[] { 28, 1 }, channel.Queries.Select(query => QuestionType(query.Query)).ToArray());
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ResolveAsync_Rfc9250Section4_3_ClosesTheConnectionWithDoqNoError()
    {
        using QuicTestServer server = DoqServer();
        DoqTestChannel channel = new(server) { Answer = Answering };

        await Resolver(new FakeDatagramConnector(channel)).ResolveAsync("example.com", CancellationToken.None);

        Assert.IsNotNull(channel.Close);
        Assert.IsNull(channel.Close.FrameType, "an application CONNECTION_CLOSE");
        Assert.AreEqual((ulong)DnsOverQuicResolver.NoError, channel.Close.ErrorCode);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ResolveAsync_Rfc9250Section4_2_1_AnswerWithANonZeroMessageId_ClosesWithDoqProtocolError()
    {
        using QuicTestServer server = DoqServer();
        DoqTestChannel channel = new(server) { Answer = (id, query) => AnswerFrames(id, Prefixed(WithId(Response(query), 7))) };

        IReadOnlyList<IPAddress> addresses = await Resolver(new FakeDatagramConnector(channel)).ResolveAsync("example.com", CancellationToken.None);

        Assert.IsEmpty(addresses);
        Assert.AreEqual((ulong)DnsOverQuicResolver.ProtocolError, channel.Close!.ErrorCode);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ResolveAsync_Rfc9250Section4_2_AnswerWithoutItsLengthPrefix_ClosesWithDoqProtocolError()
    {
        using QuicTestServer server = DoqServer();
        DoqTestChannel channel = new(server) { Answer = (id, query) => AnswerFrames(id, Response(query)) };

        IReadOnlyList<IPAddress> addresses = await Resolver(new FakeDatagramConnector(channel)).ResolveAsync("example.com", CancellationToken.None);

        Assert.IsEmpty(addresses);
        Assert.AreEqual((ulong)DnsOverQuicResolver.ProtocolError, channel.Close!.ErrorCode);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ResolveAsync_Rfc9250Section4_2_AnswerShorterThanAPrefixAndAnId_ClosesWithDoqProtocolError()
    {
        using QuicTestServer server = DoqServer();
        DoqTestChannel channel = new(server) { Answer = (id, _) => AnswerFrames(id, [0x00, 0x01, 0x00]) };

        IReadOnlyList<IPAddress> addresses = await Resolver(new FakeDatagramConnector(channel)).ResolveAsync("example.com", CancellationToken.None);

        Assert.IsEmpty(addresses);
        Assert.AreEqual((ulong)DnsOverQuicResolver.ProtocolError, channel.Close!.ErrorCode);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ResolveAsync_ServerResetsAQueryStream_ReturnsNoAddresses()
    {
        using QuicTestServer server = DoqServer();
        DoqTestChannel channel = new(server) { Answer = (id, _) => [new QuicResetStreamFrame(id, (ulong)DnsOverQuicResolver.ProtocolError, 0)] };

        IReadOnlyList<IPAddress> addresses = await Resolver(new FakeDatagramConnector(channel)).ResolveAsync("example.com", CancellationToken.None);

        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ResolveAsync_HandshakeFails_ReturnsNoAddressesAndSendsNoQuery()
    {
        using QuicTestServer server = new() { CloseAfterClientHello = 0x0a };
        DoqTestChannel channel = new(server) { Answer = Answering };

        IReadOnlyList<IPAddress> addresses = await Resolver(new FakeDatagramConnector(channel)).ResolveAsync("example.com", CancellationToken.None);

        Assert.IsEmpty(addresses);
        Assert.IsEmpty(channel.Queries);
    }

    [TestMethod]
    public async Task ResolveAsync_ChannelCannotBeOpened_ReturnsNoAddresses()
    {
        FakeDatagramConnector connector = new(null);

        IReadOnlyList<IPAddress> addresses = await Resolver(connector).ResolveAsync("example.com", CancellationToken.None);

        Assert.IsEmpty(addresses);
        Assert.HasCount(1, connector.Opens);
    }

    [TestMethod]
    public async Task ResolveAsync_AddressLiteral_ReturnsItWithoutOpeningAChannel()
    {
        FakeDatagramConnector connector = new(null);

        IReadOnlyList<IPAddress> addresses = await Resolver(connector).ResolveAsync("[2001:db8::1]", CancellationToken.None);

        CollectionAssert.AreEqual(new[] { V6 }, addresses.ToArray());
        Assert.IsEmpty(connector.Opens);
    }

    [TestMethod]
    public async Task ResolveAsync_NameDnsCannotCarry_ReturnsNoAddressesWithoutOpeningAChannel()
    {
        FakeDatagramConnector connector = new(null);

        IReadOnlyList<IPAddress> addresses = await Resolver(connector).ResolveAsync("a..example", CancellationToken.None);

        Assert.IsEmpty(addresses);
        Assert.IsEmpty(connector.Opens);
    }

    private static DnsOverQuicResolver Resolver(FakeDatagramConnector connector) =>
        new(connector, new QuicTestVerifier(), Server, TimeProvider.System);

    private static QuicTestServer DoqServer() => new()
    {
        ApplicationProtocol = DnsOverQuicResolver.ApplicationProtocol,
        ConfigureTransportParameters = parameters => parameters with
        {
            InitialMaxStreamDataBidiLocal = 1 << 16,
            InitialMaxStreamDataBidiRemote = 1 << 16,
            InitialMaxStreamsBidi = 8,
        },
    };

    // Answers each query with one record of the type it asked for, length-prefixed, then FIN.
    private static QuicFrame[] Answering(ulong streamId, byte[] query) => AnswerFrames(streamId, Prefixed(Response(query)));

    private static QuicFrame[] AnswerFrames(ulong streamId, byte[] bytes) => [new QuicStreamFrame(streamId, 0, bytes, IsFin: true)];

    private static ushort QuestionType(byte[] prefixedQuery) => BinaryPrimitives.ReadUInt16BigEndian(prefixedQuery.AsSpan(prefixedQuery.Length - 4));

    // The query's message turned into a response with one answer record whose owner points at the question.
    private static byte[] Response(byte[] prefixedQuery)
    {
        byte[] message = prefixedQuery[2..];
        ushort type = QuestionType(prefixedQuery);
        byte[] data = type == 28 ? V6.GetAddressBytes() : V4.GetAddressBytes();
        byte[] record = [0xC0, 0x0C, (byte)(type >> 8), (byte)type, 0x00, 0x01, 0x00, 0x00, 0x0E, 0x10, 0x00, (byte)data.Length, .. data];
        byte[] response = [.. message, .. record];
        response[2] = 0x81;
        response[3] = 0x80;
        response[7] = 0x01;
        return response;
    }

    private static byte[] WithId(byte[] message, ushort id)
    {
        BinaryPrimitives.WriteUInt16BigEndian(message, id);
        return message;
    }

    private static byte[] Prefixed(byte[] message)
    {
        byte[] prefixed = new byte[2 + message.Length];
        BinaryPrimitives.WriteUInt16BigEndian(prefixed, (ushort)message.Length);
        message.CopyTo(prefixed, 2);
        return prefixed;
    }
}
