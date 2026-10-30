using Curl.Extension.WireGuard.IpStack;

namespace Curl.Extension.WireGuard.UnitTests.IpStack;

[TestClass]
public sealed class UserSpaceTcpClientTests
{
    [TestMethod]
    public async Task ConnectAsync_SynAcknowledged_GivesAStreamFromAnEphemeralPort()
    {
        ScriptedTcpPeer peer = new();
        var connecting = UserSpaceTcpClient.ConnectAsync(peer.Channel, peer.Client.Address, peer.Server, 1400, peer.Clock, CancellationToken.None);

        var bytes = await peer.Channel.NextSentAsync();
        IpPacketCodec.TryParse(bytes, out var packet);
        TcpSegmentCodec.TryParse(packet!.Payload, packet.Source, packet.Destination, out var synchronize);
        ScriptedTcpPeer answering = new(new(peer.Client.Address, synchronize!.SourcePort), peer.Server);
        peer.Channel.Deliver(answering.Packet(answering.Segment(TcpFlags.Syn | TcpFlags.Ack, 1, synchronize.SequenceNumber + 1)));
        var stream = await connecting;

        Assert.IsInstanceOfType<TcpConnectionStream>(stream);
        Assert.IsGreaterThanOrEqualTo(UserSpaceTcpClient.FirstEphemeralPort, (int)synchronize.SourcePort);
        Assert.AreEqual((ushort)1400, synchronize.MaximumSegmentSize);
    }
}
