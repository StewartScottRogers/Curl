using System.Net;
using Curl.Extended.Protocol.DnsQuery.Client;
using Curl.Extended.Protocol.DnsQuery.Transport.Udp;
using Curl.Extended.Protocol.DnsQuery.UnitTests.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Transport;

/// <summary>Checks that a UDP reply is taken only from the server queried, by address and port (RFC 5452 section 9.1).</summary>
[TestClass]
public sealed class DnsUdpSourceEndpointTests
{
    private static readonly IPEndPoint Server = new(IPAddress.Parse("192.0.2.53"), 53);
    private static readonly byte[] Query = new DnsWire().Header(0x1234, 0x0100, 1, 0, 0, 0).Question(DnsWire.Name("example.com"), 1, 1).ToArray();
    private static readonly byte[] Genuine = DnsWire.SingleAnswer(1, [1, 2, 3, 4]);
    private static readonly byte[] Spoofed = DnsWire.SingleAnswer(1, [6, 6, 6, 6]);

    [TestMethod]
    public async Task ExchangeAsync_ReplyFromAnotherAddress_IsIgnoredAndTheGenuineReplyAfterItTaken()
    {
        byte[] answer = await ExchangeAsync((Spoofed, new IPEndPoint(IPAddress.Parse("192.0.2.66"), 53)), (Genuine, Server));

        CollectionAssert.AreEqual(Genuine, answer);
    }

    [TestMethod]
    public async Task ExchangeAsync_ReplyFromAnotherPort_IsIgnoredAndTheGenuineReplyAfterItTaken()
    {
        byte[] answer = await ExchangeAsync((Spoofed, new IPEndPoint(Server.Address, 29062)), (Genuine, Server));

        CollectionAssert.AreEqual(Genuine, answer);
    }

    [TestMethod]
    public async Task ExchangeAsync_ReplyFromANonIpEndPoint_IsIgnored()
    {
        byte[] answer = await ExchangeAsync((Spoofed, new DnsEndPoint("192.0.2.53", 53)), (Genuine, Server));

        CollectionAssert.AreEqual(Genuine, answer);
    }

    [TestMethod]
    public async Task ExchangeAsync_OnlySpoofedReplies_TimesOutLikeALostReply()
    {
        var channel = new FakeChannel(Server, (Spoofed, new IPEndPoint(IPAddress.Parse("192.0.2.66"), 53)), (Spoofed, new IPEndPoint(Server.Address, 29062)));

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsUdpTransport(new FakeConnector(channel), new NoTransport()).ExchangeAsync("h", 53, Query, ContextOf()));

        Assert.AreEqual(CurlExitCode.OperationTimedOut, failure.ExitCode);
        Assert.AreEqual(DnsUdpTransport.Attempts, channel.Sent);
    }

    [TestMethod]
    public async Task ExchangeAsync_ReplyFromTheServerInIpv4MappedIpv6Form_IsTaken()
    {
        byte[] answer = await ExchangeAsync((Genuine, new IPEndPoint(Server.Address.MapToIPv6(), 53)));

        CollectionAssert.AreEqual(Genuine, answer);
    }

    [TestMethod]
    public async Task ExchangeAsync_ServerEndPointInIpv4MappedIpv6Form_TakesAReplyFromItsIpv4Form()
    {
        var channel = new FakeChannel(new IPEndPoint(Server.Address.MapToIPv6(), 53), (Genuine, Server));

        byte[] answer = await new DnsUdpTransport(new FakeConnector(channel), new NoTransport()).ExchangeAsync("h", 53, Query, ContextOf());

        CollectionAssert.AreEqual(Genuine, answer);
    }

    private static async Task<byte[]> ExchangeAsync(params (byte[] Datagram, EndPoint Source)[] replies) =>
        await new DnsUdpTransport(new FakeConnector(new FakeChannel(Server, replies)), new NoTransport()).ExchangeAsync("h", 53, Query, ContextOf());

    private static TransferContext ContextOf() =>
        new() { Url = CurlUrl.Parse("dns://h/example.com"), Output = new MemoryStream(), TimeProvider = new ImmediateTimeProvider(), Events = NoTransferEvents.Instance };

    private sealed class FakeConnector(FakeChannel channel) : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            ValueTask.FromResult(DatagramOpenResult.Opened(channel));
    }

    /// <summary>Gives its replies, each from its own source, after the first send, and again after every later send.</summary>
    private sealed class FakeChannel(EndPoint server, params (byte[] Datagram, EndPoint Source)[] replies) : IDatagramChannel
    {
        private readonly Queue<(byte[] Datagram, EndPoint Source)> queued = new();

        public int Sent { get; private set; }

        public EndPoint ServerEndPoint => server;

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
        {
            Sent++;
            foreach ((byte[], EndPoint) reply in replies)
            {
                queued.Enqueue(reply);
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (!queued.TryDequeue(out (byte[] Datagram, EndPoint Source) reply))
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return default!;
            }

            reply.Datagram.CopyTo(buffer);
            return new DatagramReceived(reply.Datagram.Length, reply.Source);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoTransport : IDnsQueryTransport
    {
        public int DefaultPort => 53;

        public ValueTask<byte[]> ExchangeAsync(string host, int port, byte[] query, ITransferContext context) =>
            throw new InvalidOperationException("The fallback is not used.");
    }
}
