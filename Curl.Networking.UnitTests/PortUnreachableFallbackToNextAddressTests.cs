using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>EXT-100: a UDP channel whose first address refuses moves on to the next resolved address, as curl does.</summary>
[TestClass]
public sealed class PortUnreachableFallbackToNextAddressTests
{
    private static readonly IPEndPoint Second = new(IPAddress.Loopback, 7);
    private static readonly IPEndPoint Third = new(IPAddress.Loopback, 8);

    [TestMethod]
    public async Task ReceiveAsync_FirstAddressRefuses_SendsAgainToTheNextAndReturnsItsReply()
    {
        var first = new ScriptedChannel(IPAddress.IPv6Loopback) { ReceiveFailure = Refused() };
        var next = new ScriptedChannel(IPAddress.Loopback) { ReceiveLength = 5 };
        await using var channel = Channel(first, [Second], _ => next);
        await channel.SendAsync(new byte[] { 1, 2 }, channel.ServerEndPoint, CancellationToken.None);

        var received = await channel.ReceiveAsync(new byte[16], CancellationToken.None);

        Assert.AreEqual(5, received.Length);
        Assert.IsTrue(first.Disposed);
        Assert.AreEqual(1, next.Sent.Count);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, next.Sent[0]);
        Assert.AreEqual(next.ServerEndPoint, channel.ServerEndPoint);
        Assert.IsNull(channel.LocalEndPoint);
    }

    [TestMethod]
    public async Task SendAsync_FirstAddressRefuses_IsSentToTheNextWithoutFailing()
    {
        var first = new ScriptedChannel(IPAddress.IPv6Loopback) { SendFailure = Refused() };
        var next = new ScriptedChannel(IPAddress.Loopback);
        await using var channel = Channel(first, [Second], _ => next);

        await channel.SendAsync(new byte[] { 9 }, channel.ServerEndPoint, CancellationToken.None);

        Assert.AreEqual(1, next.Sent.Count);
    }

    [TestMethod]
    public async Task ReceiveAsync_NextAddressCannotBeOpenedOrReplayed_SkipsItAndUsesTheOneAfter()
    {
        var first = new ScriptedChannel(IPAddress.IPv6Loopback) { ReceiveFailure = Refused() };
        var replayFails = new ScriptedChannel(IPAddress.Loopback) { SendFailure = Refused() };
        var good = new ScriptedChannel(IPAddress.Loopback) { ReceiveLength = 3 };
        await using var channel = Channel(
            first,
            [Second, Third, new IPEndPoint(IPAddress.Loopback, 9)],
            endPoint => endPoint.Port switch { 7 => throw new SocketException((int)SocketError.AddressFamilyNotSupported), 8 => replayFails, _ => good });
        await channel.SendAsync(new byte[] { 1 }, channel.ServerEndPoint, CancellationToken.None);

        var received = await channel.ReceiveAsync(new byte[16], CancellationToken.None);

        Assert.AreEqual(3, received.Length);
        Assert.IsTrue(replayFails.Disposed);
    }

    [TestMethod]
    public async Task ReceiveAsync_EveryAddressRefuses_ThrowsCouldntConnectText()
    {
        var first = new ScriptedChannel(IPAddress.IPv6Loopback) { ReceiveFailure = Refused() };
        await using var channel = Channel(first, [Second], _ => new ScriptedChannel(IPAddress.Loopback) { ReceiveFailure = Refused() });
        await channel.SendAsync(new byte[] { 1 }, channel.ServerEndPoint, CancellationToken.None);

        var refused = await Assert.ThrowsExactlyAsync<DatagramPortUnreachableException>(
            async () => await channel.ReceiveAsync(new byte[16], CancellationToken.None));

        Assert.AreEqual("failed", refused.Message);
    }

    [TestMethod]
    public async Task ReceiveAsync_RefusesAfterAReply_IsNotMovedToAnotherAddress()
    {
        var first = new ScriptedChannel(IPAddress.IPv6Loopback) { ReceiveLength = 2 };
        await using var channel = Channel(first, [Second], _ => throw new InvalidOperationException("must not open"));
        await channel.ReceiveAsync(new byte[16], CancellationToken.None);
        first.ReceiveFailure = Refused();

        await Assert.ThrowsExactlyAsync<SocketException>(
            async () => await channel.ReceiveAsync(new byte[16], CancellationToken.None));
    }

    [TestMethod]
    public async Task SendAsync_AfterAReply_IsNotRecordedForReplay()
    {
        var first = new ScriptedChannel(IPAddress.IPv6Loopback) { ReceiveLength = 2 };
        await using var channel = Channel(first, [Second], _ => throw new InvalidOperationException("must not open"));
        await channel.ReceiveAsync(new byte[16], CancellationToken.None);

        await channel.SendAsync(new byte[] { 1 }, channel.ServerEndPoint, CancellationToken.None);

        Assert.AreEqual(1, first.Sent.Count);
    }

    [TestMethod]
    public async Task SendAsync_AfterASwitch_AddressedToTheRefusedEndPoint_GoesToTheNewOne()
    {
        var first = new ScriptedChannel(IPAddress.IPv6Loopback) { SendFailure = Refused() };
        var next = new ScriptedChannel(IPAddress.Loopback);
        await using var channel = Channel(first, [Second], _ => next);
        var refusedEndPoint = channel.ServerEndPoint;
        await channel.SendAsync(new byte[] { 1 }, refusedEndPoint, CancellationToken.None);

        await channel.SendAsync(new byte[] { 2 }, refusedEndPoint, CancellationToken.None);
        await channel.SendAsync(new byte[] { 3 }, Third, CancellationToken.None);

        Assert.AreEqual(3, next.Sent.Count);
        Assert.AreEqual(next.ServerEndPoint, next.Destinations[1]);
        Assert.AreEqual(Third, next.Destinations[2]);
    }

    [TestMethod]
    public async Task SendAsync_ResendCancelled_DisposesTheOpenedChannelAndPropagates()
    {
        var first = new ScriptedChannel(IPAddress.IPv6Loopback) { SendFailure = Refused() };
        var next = new ScriptedChannel(IPAddress.Loopback) { SendFailure = new OperationCanceledException() };
        await using var channel = Channel(first, [Second], _ => next);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await channel.SendAsync(new byte[] { 1 }, channel.ServerEndPoint, CancellationToken.None));

        Assert.IsTrue(next.Disposed);
    }

    private static SocketException Refused() => new((int)SocketError.ConnectionRefused);

    private static PortUnreachableReportingDatagramChannel Channel(
        IDatagramChannel first,
        IReadOnlyList<IPEndPoint> alternatives,
        Func<IPEndPoint, IDatagramChannel> open) =>
        new(first, () => "failed", alternatives, open);

    private sealed class ScriptedChannel(IPAddress address) : IDatagramChannel
    {
        public EndPoint ServerEndPoint { get; } = new IPEndPoint(address, 53);

        public EndPoint? LocalEndPoint => null;

        public Exception? SendFailure { get; set; }

        public SocketException? ReceiveFailure { get; set; }

        public int ReceiveLength { get; set; }

        public List<byte[]> Sent { get; } = [];

        public List<EndPoint> Destinations { get; } = [];

        public bool Disposed { get; private set; }

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
        {
            if (SendFailure is not null)
            {
                return ValueTask.FromException(SendFailure);
            }

            Sent.Add(datagram.ToArray());
            Destinations.Add(destination);
            return ValueTask.CompletedTask;
        }

        public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            ReceiveFailure is not null
                ? ValueTask.FromException<DatagramReceived>(ReceiveFailure)
                : ValueTask.FromResult(new DatagramReceived(ReceiveLength, ServerEndPoint));

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
