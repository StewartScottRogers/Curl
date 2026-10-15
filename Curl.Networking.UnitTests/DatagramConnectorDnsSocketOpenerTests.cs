using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

[TestClass]
public sealed class DatagramConnectorDnsSocketOpenerTests
{
    private static readonly IPEndPoint Server = new(IPAddress.Loopback, 5353);

    [TestMethod]
    public async Task OpenDatagramChannel_SentTwice_OpensThroughTheConnectorOnceAndForwardsEachDatagram()
    {
        EchoConnector connector = new();
        IDatagramChannel channel = new DatagramConnectorDnsSocketOpener(connector).OpenDatagramChannel(Server, IPAddress.Any);

        await channel.SendAsync(new byte[] { 1, 2 }, Server, CancellationToken.None);
        await channel.SendAsync(new byte[] { 3 }, Server, CancellationToken.None);
        byte[] buffer = new byte[8];
        DatagramReceived received = await channel.ReceiveAsync(buffer, CancellationToken.None);
        await channel.DisposeAsync();

        Assert.AreEqual(Server, channel.ServerEndPoint);
        CollectionAssert.AreEqual(new[] { "127.0.0.1:5353" }, connector.Opened);
        Assert.AreEqual(1, received.Length);
        Assert.AreEqual(3, buffer[0]);
        Assert.IsTrue(connector.Channel!.Disposed);
    }

    [TestMethod]
    public async Task OpenDatagramChannel_WhenTheConnectorRefuses_SendThrowsSocketException()
    {
        IDatagramChannel channel = new DatagramConnectorDnsSocketOpener(new EchoConnector { Refuses = true }).OpenDatagramChannel(Server, IPAddress.Any);

        await Assert.ThrowsExactlyAsync<SocketException>(async () => await channel.SendAsync(new byte[] { 1 }, Server, CancellationToken.None));
    }

    [TestMethod]
    public async Task OpenDatagramChannel_ReceivedBeforeAnySend_ThrowsSocketExceptionAndDisposesNothing()
    {
        IDatagramChannel channel = new DatagramConnectorDnsSocketOpener(new EchoConnector()).OpenDatagramChannel(Server, IPAddress.Any);

        await Assert.ThrowsExactlyAsync<SocketException>(async () => await channel.ReceiveAsync(new byte[8], CancellationToken.None));
        await channel.DisposeAsync();
    }

    [TestMethod]
    public async Task ConnectStreamAsync_Always_ThrowsSocketException()
    {
        DatagramConnectorDnsSocketOpener opener = new(new EchoConnector());

        await Assert.ThrowsExactlyAsync<SocketException>(async () => await opener.ConnectStreamAsync(Server, IPAddress.Any, CancellationToken.None));
    }

    [TestMethod]
    public async Task DnsServerResolver_OverADatagramConnectorGivenALiteral_AnswersWithoutOpeningAChannel()
    {
        EchoConnector connector = new();
        DnsServerResolverOptions options = new("127.0.0.1:5353", null, null, null);
        DnsServerResolver first = new(options, TimeProvider.System, connector);
        DnsServerResolver second = new(options, TimeProvider.System, connector);

        IReadOnlyList<IPAddress> firstAddresses = await first.ResolveAsync("192.0.2.1", CancellationToken.None);
        IReadOnlyList<IPAddress> secondAddresses = await second.ResolveAsync("2001:db8::1", CancellationToken.None);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("192.0.2.1") }, firstAddresses.ToArray());
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("2001:db8::1") }, secondAddresses.ToArray());
        Assert.IsEmpty(connector.Opened);
    }

    /// <summary>A connector whose channel sends each datagram back; or that refuses.</summary>
    private sealed class EchoConnector : IDatagramConnector
    {
        public bool Refuses { get; init; }

        public List<string> Opened { get; } = [];

        public EchoChannel? Channel { get; private set; }

        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
        {
            Opened.Add($"{host}:{port}");
            Channel = new EchoChannel(new IPEndPoint(IPAddress.Parse(host), port));
            return ValueTask.FromResult(Refuses
                ? DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, "refused")
                : DatagramOpenResult.Opened(Channel));
        }
    }

    private sealed class EchoChannel(IPEndPoint endPoint) : IDatagramChannel
    {
        private byte[] _last = [];

        public bool Disposed { get; private set; }

        public EndPoint ServerEndPoint => endPoint;

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
        {
            _last = datagram.ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            _last.CopyTo(buffer);
            return ValueTask.FromResult(new DatagramReceived(_last.Length, endPoint));
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
