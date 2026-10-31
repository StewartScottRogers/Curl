using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.IpStack;
using Curl.Extension.WireGuard.IpStack.Udp;
using Curl.Extension.WireGuard.Tunnel;
using Curl.Extension.WireGuard.UnitTests.IpStack;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.UnitTests.Tunnel;

/// <summary>Checks the tunnel's datagram connector against a peer written from the whitepaper, with no network.</summary>
[TestClass]
public sealed class WireGuardDatagramConnectorTests
{
    [TestMethod]
    public async Task OpenAsync_AddressInsideAllowedIps_CarriesUdpSealedAsTransportData()
    {
        TunnelTestPeer peer = new();
        WireGuardDatagramConnector connector = Connector(peer, TunnelTestPeer.Configuration());

        var opening = connector.OpenAsync("10.0.0.1", 443, CancellationToken.None).AsTask();
        await peer.AcceptHandshakeAsync();
        IDatagramChannel channel = (await opening).Channel!;
        await channel.SendAsync("hi"u8.ToArray(), channel.ServerEndPoint, CancellationToken.None);
        byte[]? opened = peer.Responder.OpenTransport(await peer.NextSentAsync());
        Assert.IsTrue(IpPacketCodec.TryParse(opened, out IpPacket? packet));
        Assert.IsTrue(UdpDatagramCodec.TryParse(packet!, out UdpDatagram? datagram));
        IPEndPoint local = new(packet!.Source, datagram!.SourcePort);
        peer.Deliver(peer.SealToInitiator(IpPacketCodec.Build(new IpPacket(packet.Destination, packet.Source, UdpDatagramCodec.UdpProtocol, UdpDatagramCodec.Build(new IPEndPoint(packet.Destination, 443), local, "ok"u8)))));
        byte[] buffer = new byte[8];
        DatagramReceived received = await channel.ReceiveAsync(buffer, CancellationToken.None);
        await channel.DisposeAsync();

        Assert.AreEqual(new WireGuardEndpoint("192.0.2.1", 51820), peer.OpenedEndpoint);
        Assert.AreEqual(IPAddress.Parse("10.0.0.2"), packet.Source);
        Assert.AreEqual(new IPEndPoint(IPAddress.Parse("10.0.0.1"), 443), channel.ServerEndPoint);
        Assert.AreEqual(local, channel.LocalEndPoint);
        Assert.IsGreaterThanOrEqualTo(UserSpaceTcpClient.FirstEphemeralPort, (int)datagram.SourcePort);
        Assert.AreEqual((ushort)443, datagram.DestinationPort);
        CollectionAssert.AreEqual("hi"u8.ToArray(), datagram.Payload);
        Assert.AreEqual("ok", System.Text.Encoding.ASCII.GetString(buffer, 0, received.Length));
        Assert.AreEqual(channel.ServerEndPoint, received.RemoteEndPoint);
        Assert.IsTrue(peer.IsDisposed);
    }

    [TestMethod]
    public async Task OpenAsync_HostNameWithNoDnsServer_CannotBeResolvedAndOpensNothing()
    {
        TunnelTestPeer peer = new();

        DatagramOpenResult result = await Connector(peer, TunnelTestPeer.Configuration()).OpenAsync("example.com", 443, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: example.com", result.ErrorMessage);
        Assert.IsNull(peer.OpenedEndpoint);
    }

    [TestMethod]
    public async Task OpenAsync_AddressNoPeerRoutes_CannotConnect()
    {
        TunnelTestPeer peer = new();

        DatagramOpenResult result = await Connector(peer, TunnelTestPeer.Configuration()).OpenAsync("[2001:db8::1]", 443, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to [2001:db8::1] port 443: no WireGuard peer's AllowedIPs and interface Address cover it", result.ErrorMessage);
        Assert.IsNull(peer.OpenedEndpoint);
    }

    [TestMethod]
    public async Task OpenAsync_UdpSocketToThePeerFails_CannotConnect()
    {
        TunnelTestPeer peer = new() { OpenFailure = new SocketException((int)SocketError.NetworkUnreachable) };

        DatagramOpenResult result = await Connector(peer, TunnelTestPeer.Configuration()).OpenAsync("10.0.0.1", 443, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        StringAssert.StartsWith(result.ErrorMessage, "Failed to connect to 10.0.0.1 port 443 through the WireGuard tunnel: ");
    }

    [TestMethod]
    public async Task OpenAsync_HandshakeFailsWithIoError_CannotConnectAndClosesTheSocket()
    {
        FailingUdp udp = new(new IOException("the socket broke"));

        DatagramOpenResult result = await Connector(udp, TunnelTestPeer.Configuration()).OpenAsync("10.0.0.1", 443, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 10.0.0.1 port 443 through the WireGuard tunnel: the socket broke", result.ErrorMessage);
        Assert.IsTrue(udp.IsDisposed);
    }

    [TestMethod]
    public async Task OpenAsync_SocketFailsDuringTheHandshake_CannotConnectAndClosesTheSocket()
    {
        FailingUdp udp = new(new SocketException((int)SocketError.ConnectionReset));

        DatagramOpenResult result = await Connector(udp, TunnelTestPeer.Configuration()).OpenAsync("10.0.0.1", 443, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.StartsWith("Failed to connect to 10.0.0.1 port 443 through the WireGuard tunnel: ", result.ErrorMessage);
        Assert.IsTrue(udp.IsDisposed);
    }

    [TestMethod]
    public async Task OpenAsync_HostNameWithAnUnreachablePeer_CannotConnectNotUnresolved()
    {
        TunnelTestPeer peer = new() { OpenFailure = new SocketException((int)SocketError.NetworkUnreachable) };

        DatagramOpenResult result = await Connector(peer, TunnelTestPeer.Configuration(extraInterfaceLine: "DNS = 10.0.0.53")).OpenAsync("example.com", 443, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.StartsWith("Failed to connect to 10.0.0.53 port 53 through the WireGuard tunnel: ", result.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenAsync_HandshakeTimesOut_CannotConnectAndClosesTheSocket()
    {
        FailingUdp udp = new(new TimeoutException("no response"));

        DatagramOpenResult result = await Connector(udp, TunnelTestPeer.Configuration()).OpenAsync("10.0.0.1", 443, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 10.0.0.1 port 443 through the WireGuard tunnel: no response", result.ErrorMessage);
        Assert.IsTrue(udp.IsDisposed);
    }

    [TestMethod]
    public async Task OpenAsync_CancelledDuringTheHandshake_ThrowsAndClosesTheSocket()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        FailingUdp udp = new(new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await Connector(udp, TunnelTestPeer.Configuration()).OpenAsync("10.0.0.1", 443, cancellation.Token));

        Assert.IsTrue(udp.IsDisposed);
    }

    [TestMethod]
    public async Task OpenAsync_NullHost_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Connector(new TunnelTestPeer(), TunnelTestPeer.Configuration()).OpenAsync(null!, 443, CancellationToken.None));
    }

    private static WireGuardDatagramConnector Connector(IWireGuardUdpOpener opener, WireGuardConfiguration configuration)
    {
        ManualTimeProvider time = new();
        RandomNumberGenerator random = RandomNumberGenerator.Create();
        return new(configuration, opener, time, random, new WireGuardConnector(configuration, opener, time, random));
    }

    /// <summary>A UDP socket to the peer whose every receive fails with one exception.</summary>
    private sealed class FailingUdp(Exception failure) : IWireGuardUdpOpener, IWireGuardUdpChannel
    {
        public bool IsDisposed { get; private set; }

        public ValueTask<IWireGuardUdpChannel> OpenAsync(WireGuardEndpoint endpoint, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IWireGuardUdpChannel>(this);

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) =>
            ValueTask.FromException<ReadOnlyMemory<byte>>(failure);

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
