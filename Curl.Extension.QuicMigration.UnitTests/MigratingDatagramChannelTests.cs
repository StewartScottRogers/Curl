using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.QuicMigration;

/// <summary>
/// Checks <see cref="MigratingDatagramChannel" /> against RFC 9000 section 9 (connection migration) and
/// draft-ietf-quic-multipath section 2 (one path without negotiation), over fake channels with no network.
/// </summary>
[TestClass]
public sealed class MigratingDatagramChannelTests
{
    private static readonly byte[] Initial = [0xC3, 0x00, 0x00, 0x00, 0x01];
    private static readonly byte[] OneRtt = [0x41, 0x0A, 0x0B];

    [TestMethod]
    public async Task SendAndReceive_AnyDatagram_PassesEveryDatagramThroughByteForByte()
    {
        FakeDatagramConnector connector = new();
        FakeDatagramChannel first = new(5000);
        first.Enqueue(Initial);
        await using MigratingDatagramChannel channel = new(first, connector, "example.com", 443);
        byte[] buffer = new byte[16];

        await channel.SendAsync(Initial, FakeDatagramChannel.Server, CancellationToken.None);
        DatagramReceived received = await channel.ReceiveAsync(buffer, CancellationToken.None);

        CollectionAssert.AreEqual(Initial, first.Sent.Single());
        CollectionAssert.AreEqual(Initial, buffer[..received.Length]);
        Assert.AreSame(FakeDatagramChannel.Server, channel.ServerEndPoint);
        Assert.AreSame(first.LocalEndPoint, channel.LocalEndPoint);
        Assert.IsFalse(channel.HandshakeComplete);
        Assert.IsEmpty(connector.Opens);
    }

    [TestMethod]
    public async Task SendOrReceive_Section9_PathLossBeforeTheHandshakeIsConfirmed_EndsTheTransferAsBefore()
    {
        FakeDatagramConnector connector = new();
        connector.Add(5001);
        FakeDatagramChannel first = new(5000) { SendFailure = new SocketException((int)SocketError.NetworkDown) };
        await using MigratingDatagramChannel channel = new(first, connector, "example.com", 443);

        await Assert.ThrowsExactlyAsync<SocketException>(async () => await channel.SendAsync(OneRtt, FakeDatagramChannel.Server, CancellationToken.None));

        Assert.IsEmpty(connector.Opens);
        Assert.AreEqual(0, channel.Migrations);
    }

    [TestMethod]
    public async Task SendOrReceive_Section9_OnlyASentOneRttPacket_IsNotAConfirmedHandshake()
    {
        FakeDatagramChannel first = new(5000);
        first.Enqueue(Initial);
        await using MigratingDatagramChannel channel = new(first, new FakeDatagramConnector(), "example.com", 443);

        await channel.SendAsync(OneRtt, FakeDatagramChannel.Server, CancellationToken.None);
        await channel.ReceiveAsync(new byte[16], CancellationToken.None);

        Assert.IsFalse(channel.HandshakeComplete);
    }

    [TestMethod]
    public async Task SendOrReceive_Section9_2_SendFailingWithLocalPathLoss_MovesToANewLocalAddressAndResendsTheSameBytes()
    {
        FakeDatagramConnector connector = new();
        FakeDatagramChannel second = connector.Add(5001);
        FakeDatagramChannel first = HandshakenChannel(5000);
        await using MigratingDatagramChannel channel = await MigratingOverAsync(first, connector);
        first.SendFailure = new SocketException((int)SocketError.NetworkUnreachable);
        byte[] packet = [0x5F, 0x01, 0x02, 0x03];

        await channel.SendAsync(packet, FakeDatagramChannel.Server, CancellationToken.None);

        CollectionAssert.AreEqual(packet, second.Sent.Single());
        Assert.IsTrue(first.IsDisposed);
        Assert.AreEqual(1, channel.Migrations);
        Assert.AreSame(second.LocalEndPoint, channel.LocalEndPoint);
        CollectionAssert.AreEqual(new[] { ("example.com", 443) }, connector.Opens);
    }

    [TestMethod]
    public async Task SendOrReceive_Section9_2_ReceiveFailingWithLocalPathLoss_MovesAndReceivesOnTheNewPath()
    {
        FakeDatagramConnector connector = new();
        FakeDatagramChannel second = connector.Add(5001);
        second.Enqueue(OneRtt);
        FakeDatagramChannel first = HandshakenChannel(5000);
        await using MigratingDatagramChannel channel = await MigratingOverAsync(first, connector);
        first.ReceiveFailure = new SocketException((int)SocketError.AddressNotAvailable);
        byte[] buffer = new byte[16];

        DatagramReceived received = await channel.ReceiveAsync(buffer, CancellationToken.None);

        CollectionAssert.AreEqual(OneRtt, buffer[..received.Length]);
        Assert.IsTrue(first.IsDisposed);
        Assert.AreEqual(1, channel.Migrations);
    }

    [TestMethod]
    public async Task SendOrReceive_Section9_AFailureThatIsNotPathLoss_EndsTheTransferAsBefore()
    {
        FakeDatagramConnector connector = new();
        connector.Add(5001);
        FakeDatagramChannel first = HandshakenChannel(5000);
        await using MigratingDatagramChannel channel = await MigratingOverAsync(first, connector);
        first.SendFailure = new SocketException((int)SocketError.ConnectionRefused);

        await Assert.ThrowsExactlyAsync<SocketException>(async () => await channel.SendAsync(OneRtt, FakeDatagramChannel.Server, CancellationToken.None));

        Assert.IsEmpty(connector.Opens);
        Assert.IsFalse(first.IsDisposed);
    }

    [TestMethod]
    public async Task SendOrReceive_Section9_2_NoNewPathOpening_EndsTheTransferWithTheOriginalFailure()
    {
        FakeDatagramConnector connector = new();
        FakeDatagramChannel first = HandshakenChannel(5000);
        await using MigratingDatagramChannel channel = await MigratingOverAsync(first, connector);
        SocketException lost = new((int)SocketError.NetworkDown);
        first.SendFailure = lost;

        SocketException thrown = await Assert.ThrowsExactlyAsync<SocketException>(async () => await channel.SendAsync(OneRtt, FakeDatagramChannel.Server, CancellationToken.None));

        Assert.AreSame(lost, thrown);
        Assert.AreEqual(0, channel.Migrations);
        Assert.HasCount(1, connector.Opens);
    }

    [TestMethod]
    public async Task SendOrReceive_MultipathSection2_AReceivePendingWhenASendMoved_CarriesOnOverTheOnePathInUse()
    {
        FakeDatagramConnector connector = new();
        FakeDatagramChannel second = connector.Add(5001);
        second.Enqueue(OneRtt);
        FakeDatagramChannel first = HandshakenChannel(5000);
        await using MigratingDatagramChannel channel = await MigratingOverAsync(first, connector);
        first.ReceiveGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] buffer = new byte[16];
        ValueTask<DatagramReceived> pending = channel.ReceiveAsync(buffer, CancellationToken.None);
        first.SendFailure = new SocketException((int)SocketError.NetworkReset);

        await channel.SendAsync(OneRtt, FakeDatagramChannel.Server, CancellationToken.None);
        first.ReceiveGate.SetResult();
        DatagramReceived received = await pending;

        CollectionAssert.AreEqual(OneRtt, buffer[..received.Length]);
        Assert.AreEqual(1, channel.Migrations);
        Assert.HasCount(1, connector.Opens);
    }

    [TestMethod]
    public async Task SendOrReceive_MultipathSection2_AfterAMove_TheOldPathCarriesNothingMore()
    {
        FakeDatagramConnector connector = new();
        FakeDatagramChannel second = connector.Add(5001);
        FakeDatagramChannel first = HandshakenChannel(5000);
        await using MigratingDatagramChannel channel = await MigratingOverAsync(first, connector);
        first.SendFailure = new SocketException((int)SocketError.HostUnreachable);
        int sentBefore = first.Sent.Count;

        await channel.SendAsync(OneRtt, FakeDatagramChannel.Server, CancellationToken.None);
        await channel.SendAsync(OneRtt, FakeDatagramChannel.Server, CancellationToken.None);

        Assert.HasCount(sentBefore, first.Sent);
        Assert.HasCount(2, second.Sent);
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesThePathInUse()
    {
        FakeDatagramChannel first = new(5000);
        MigratingDatagramChannel channel = new(first, new FakeDatagramConnector(), "example.com", 443);

        await channel.DisposeAsync();

        Assert.IsTrue(first.IsDisposed);
    }

    private static FakeDatagramChannel HandshakenChannel(int localPort)
    {
        FakeDatagramChannel channel = new(localPort);
        channel.Enqueue(OneRtt);
        return channel;
    }

    private static async Task<MigratingDatagramChannel> MigratingOverAsync(FakeDatagramChannel first, FakeDatagramConnector connector)
    {
        MigratingDatagramChannel channel = new(first, connector, "example.com", 443);
        await channel.SendAsync(OneRtt, FakeDatagramChannel.Server, CancellationToken.None);
        await channel.ReceiveAsync(new byte[16], CancellationToken.None);
        Assert.IsTrue(channel.HandshakeComplete);
        return channel;
    }
}
