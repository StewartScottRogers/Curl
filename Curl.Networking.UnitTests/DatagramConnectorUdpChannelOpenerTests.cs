using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

[TestClass]
public sealed class DatagramConnectorUdpChannelOpenerTests
{
    private static readonly IPEndPoint Server = new(IPAddress.Parse("127.0.0.1"), 4433);

    [TestMethod]
    public async Task Open_SentTwiceThenReceived_OpensTheEndPointsHostAndPortOnceAndForwardsEachDatagram()
    {
        EchoConnector connector = new();
        IDatagramChannel channel = new DatagramConnectorUdpChannelOpener(connector).Open(Server);

        await channel.SendAsync(new byte[] { 1, 2 }, Server, CancellationToken.None);
        await channel.SendAsync(new byte[] { 3 }, Server, CancellationToken.None);
        byte[] buffer = new byte[8];
        DatagramReceived received = await channel.ReceiveAsync(buffer, CancellationToken.None);
        await channel.DisposeAsync();

        Assert.AreEqual(Server, channel.ServerEndPoint);
        CollectionAssert.AreEqual(new[] { "127.0.0.1:4433" }, connector.Opened);
        CollectionAssert.AreEqual(new[] { 2, 1 }, connector.Channel!.Sent.Select(datagram => datagram.Length).ToArray());
        Assert.AreEqual(1, received.Length);
        Assert.AreEqual(3, buffer[0]);
        Assert.IsTrue(connector.Channel.Disposed);
    }

    [TestMethod]
    public async Task Open_ReceivedBeforeAnySend_OpensThroughTheConnector()
    {
        EchoConnector connector = new();
        IDatagramChannel channel = new DatagramConnectorUdpChannelOpener(connector).Open(Server);

        DatagramReceived received = await channel.ReceiveAsync(new byte[8], CancellationToken.None);

        Assert.AreEqual(0, received.Length);
        CollectionAssert.AreEqual(new[] { "127.0.0.1:4433" }, connector.Opened);
    }

    [TestMethod]
    public async Task LocalEndPoint_BeforeAndAfterTheOpen_IsNullThenTheOpenedChannels()
    {
        EchoConnector connector = new();
        IDatagramChannel channel = new DatagramConnectorUdpChannelOpener(connector).Open(Server);

        EndPoint? before = channel.LocalEndPoint;
        await channel.SendAsync(new byte[] { 1 }, Server, CancellationToken.None);
        EndPoint? after = channel.LocalEndPoint;

        Assert.IsNull(before);
        Assert.AreEqual(EchoChannel.Local, after);
    }

    [TestMethod]
    public async Task DisposeAsync_BeforeTheOpen_OpensNothing()
    {
        EchoConnector connector = new();
        IDatagramChannel channel = new DatagramConnectorUdpChannelOpener(connector).Open(Server);

        await channel.DisposeAsync();

        Assert.IsEmpty(connector.Opened);
    }

    [TestMethod]
    public async Task Open_WhenTheConnectorRefuses_SendThrowsConnectionRefused()
    {
        IDatagramChannel channel = new DatagramConnectorUdpChannelOpener(new EchoConnector { Refuses = true }).Open(Server);

        SocketException thrown = await Assert.ThrowsExactlyAsync<SocketException>(async () => await channel.SendAsync(new byte[] { 1 }, Server, CancellationToken.None));

        Assert.AreEqual(SocketError.ConnectionRefused, thrown.SocketErrorCode);
    }

    [TestMethod]
    public void Open_WithANullEndPoint_Throws()
    {
        DatagramConnectorUdpChannelOpener opener = new(new EchoConnector());

        Assert.ThrowsExactly<ArgumentNullException>(() => opener.Open(null!));
    }

    [TestMethod]
    public async Task OpenFrom_WithALocalEnd_OpensTheServerThroughTheConnectorAndWritesNoBindLine()
    {
        EchoConnector connector = new();
        RecordingTransferEvents events = new();
        IDatagramChannel channel = new DatagramConnectorUdpChannelOpener(connector).OpenFrom(Server, new IPEndPoint(IPAddress.Loopback, 40000), 5, events);

        await channel.SendAsync(new byte[] { 1 }, Server, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "127.0.0.1:4433" }, connector.Opened);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public async Task OpenFromDeviceAsync_WithADevice_OpensTheServerThroughTheConnectorWithoutChoosingALocalEnd()
    {
        EchoConnector connector = new();
        bool chose = false;
        IDatagramChannel channel = await new DatagramConnectorUdpChannelOpener(connector).OpenFromDeviceAsync(
            Server,
            "eth0",
            bindsAddressAfterDevice: true,
            _ =>
            {
                chose = true;
                return ValueTask.FromResult(new IPEndPoint(IPAddress.Loopback, 0));
            },
            1,
            new RecordingTransferEvents(),
            CancellationToken.None);

        await channel.SendAsync(new byte[] { 1 }, Server, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "127.0.0.1:4433" }, connector.Opened);
        Assert.IsFalse(chose);
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
        public static readonly IPEndPoint Local = new(IPAddress.Loopback, 50000);

        private byte[] _last = [];

        public List<byte[]> Sent { get; } = [];

        public bool Disposed { get; private set; }

        public EndPoint ServerEndPoint => endPoint;

        public EndPoint? LocalEndPoint => Local;

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
        {
            _last = datagram.ToArray();
            Sent.Add(_last);
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
