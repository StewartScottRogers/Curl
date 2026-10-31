using System.Buffers.Binary;
using Curl.Extended.Protocol.DnsQuery.Client;
using Curl.Extended.Protocol.DnsQuery.Transport.Quic;
using Curl.Extended.Protocol.DnsQuery.UnitTests.Transport.Tls;
using Curl.Extended.Protocol.DnsQuery.UnitTests.Wire;
using Curl.Protocol.Abstractions;
using Curl.Quic;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Transport.Quic;

/// <summary>
/// Checks DNS over QUIC (RFC 9250) with the real QUIC client against an in-memory QUIC server on a fake datagram
/// channel: ALPN <c>doq</c> and port 853 (section 4.1.1), the query on a client bidirectional stream ended with FIN
/// and its two-octet length prefix (section 4.2), Message ID 0 (section 4.2.1), and closing with DOQ_NO_ERROR or
/// DOQ_PROTOCOL_ERROR (sections 4.3 and 4.5).
/// </summary>
[TestClass]
public sealed class DnsQuicTransportTests
{
    private const int HangTimeoutMilliseconds = 120_000;

    private static readonly byte[] Query = new DnsWire().Header(0x1234, 0x0100, 1, 0, 0, 0).Question(DnsWire.Name("example.com"), 1, 1).ToArray();

    [TestMethod]
    public void DefaultPort_Rfc9250Section4_1_1_Is853() =>
        Assert.AreEqual(853, new DnsQuicTransport(new FakeDatagramConnector(null), new HandshakingTlsProvider()).DefaultPort);

    [TestMethod]
    public void CreateQuicSettings_Rfc9250Section4_1_1_OffersOnlyTheDoqTokenAndNamesTheServer()
    {
        QuicClientSettings settings = DnsQuicTransport.CreateQuicSettings("dns.example");

        CollectionAssert.AreEqual(new[] { "doq" }, settings.Tls.ApplicationProtocols.ToArray());
        Assert.AreEqual("dns.example", settings.Tls.ServerName);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExchangeAsync_Rfc9250Section4_2_SendsTheQueryOnAClientBidirectionalStreamWithMessageId0AndFin()
    {
        using QuicTestServer server = DoqServer();
        DoqTestChannel channel = new(server) { Answer = Answering };
        FakeDatagramConnector connector = new(channel);
        var tls = new HandshakingTlsProvider();

        byte[] answer = await new DnsQuicTransport(connector, tls).ExchangeAsync("dns.example", 853, Query, ContextOf());

        Assert.AreEqual(("dns.example", 853), connector.Opens.Single());
        Assert.IsTrue(server.ClientFinishedReceived);
        Assert.AreEqual("dns.example", tls.TargetHost, "the certificate is judged by the TLS provider against the server's host");
        (ulong streamId, byte[] sent) = channel.Queries.Single();
        Assert.AreEqual(0UL, streamId, "the first client-initiated bidirectional stream");
        Assert.AreEqual(Query.Length, BinaryPrimitives.ReadUInt16BigEndian(sent), "two-octet length prefix");
        Assert.AreEqual(0, BinaryPrimitives.ReadUInt16BigEndian(sent.AsSpan(2)), "Message ID 0 (section 4.2.1)");
        CollectionAssert.AreEqual(Query[2..], sent[4..]);
        Assert.AreEqual(0x1234, BinaryPrimitives.ReadUInt16BigEndian(answer), "the answer carries the query's own ID back to the caller");
        CollectionAssert.AreEqual(Response(sent)[2..], answer[2..]);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExchangeAsync_Rfc9250Section4_3_ClosesTheConnectionWithDoqNoError()
    {
        using QuicTestServer server = DoqServer();
        DoqTestChannel channel = new(server) { Answer = Answering };

        await new DnsQuicTransport(new FakeDatagramConnector(channel), new HandshakingTlsProvider()).ExchangeAsync("dns.example", 853, Query, ContextOf());

        Assert.IsNotNull(channel.Close);
        Assert.IsNull(channel.Close.FrameType, "an application CONNECTION_CLOSE");
        Assert.AreEqual((ulong)DnsQuicTransport.NoError, channel.Close.ErrorCode);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    [DataRow(7, true, DisplayName = "Message ID not 0 (section 4.2.1)")]
    [DataRow(0, false, DisplayName = "no length prefix (section 4.2)")]
    public async Task ExchangeAsync_Rfc9250Section4_3_3_MalformedAnswer_ClosesWithDoqProtocolErrorAndFailsWithExit8(int messageId, bool prefixed)
    {
        using QuicTestServer server = DoqServer();
        DoqTestChannel channel = new(server)
        {
            Answer = (id, query) =>
            {
                byte[] response = Response(query);
                BinaryPrimitives.WriteUInt16BigEndian(response, (ushort)messageId);
                return AnswerFrames(id, prefixed ? Prefixed(response) : response);
            },
        };

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsQuicTransport(new FakeDatagramConnector(channel), new HandshakingTlsProvider()).ExchangeAsync("dns.example", 853, Query, ContextOf()));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual((ulong)DnsQuicTransport.ProtocolError, channel.Close!.ErrorCode);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExchangeAsync_AnswerShorterThanAPrefixAndAnId_FailsWithExit8()
    {
        using QuicTestServer server = DoqServer();
        DoqTestChannel channel = new(server) { Answer = (id, _) => AnswerFrames(id, [0x00, 0x01, 0x00]) };

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsQuicTransport(new FakeDatagramConnector(channel), new HandshakingTlsProvider()).ExchangeAsync("dns.example", 853, Query, ContextOf()));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExchangeAsync_ServerResetsTheQueryStream_FailsWithExit56()
    {
        using QuicTestServer server = DoqServer();
        DoqTestChannel channel = new(server) { Answer = (id, _) => [new QuicResetStreamFrame(id, (ulong)DnsQuicTransport.ProtocolError, 0)] };

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsQuicTransport(new FakeDatagramConnector(channel), new HandshakingTlsProvider()).ExchangeAsync("dns.example", 853, Query, ContextOf()));

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExchangeAsync_HandshakeFails_FailsWithTheHandshakesExitCodeAndSendsNoQuery()
    {
        using QuicTestServer server = new() { CloseAfterClientHello = 0x0a };
        DoqTestChannel channel = new(server) { Answer = Answering };

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsQuicTransport(new FakeDatagramConnector(channel), new HandshakingTlsProvider()).ExchangeAsync("dns.example", 853, Query, ContextOf()));

        Assert.AreNotEqual(CurlExitCode.Ok, failure.ExitCode);
        Assert.IsEmpty(channel.Queries);
    }

    [TestMethod]
    public async Task ExchangeAsync_ChannelCannotBeOpened_FailsWithTheConnectorsExitCode()
    {
        FakeDatagramConnector connector = new(null);

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsQuicTransport(connector, new HandshakingTlsProvider()).ExchangeAsync("dns.example", 853, Query, ContextOf()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode);
        Assert.AreEqual("Could not connect to server", failure.Message);
    }

    private static TransferContext ContextOf() => new() { Url = CurlUrl.Parse("dns://dns.example/example.com"), Output = new MemoryStream(), TimeProvider = TimeProvider.System };

    private static QuicTestServer DoqServer() => new()
    {
        ApplicationProtocol = DnsQuicTransport.ApplicationProtocol,
        ConfigureTransportParameters = parameters => parameters with
        {
            InitialMaxStreamDataBidiLocal = 1 << 16,
            InitialMaxStreamDataBidiRemote = 1 << 16,
            InitialMaxStreamsBidi = 8,
        },
    };

    // Answers the query with one A record, length-prefixed, then FIN.
    private static QuicFrame[] Answering(ulong streamId, byte[] query) => AnswerFrames(streamId, Prefixed(Response(query)));

    private static QuicFrame[] AnswerFrames(ulong streamId, byte[] bytes) => [new QuicStreamFrame(streamId, 0, bytes, IsFin: true)];

    // The prefixed query's message turned into a response with one A record whose owner points at the question.
    private static byte[] Response(byte[] prefixedQuery)
    {
        byte[] record = [0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x0E, 0x10, 0x00, 0x04, 192, 0, 2, 1];
        byte[] response = [.. prefixedQuery[2..], .. record];
        response[2] = 0x81;
        response[3] = 0x80;
        response[7] = 0x01;
        return response;
    }

    private static byte[] Prefixed(byte[] message) => [.. DnsWire.UInt16(message.Length), .. message];
}
