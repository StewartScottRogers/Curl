using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.IpStack;
using Curl.Extension.WireGuard.Tunnel;
using Curl.Extension.WireGuard.UnitTests.IpStack;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.UnitTests.Tunnel;

/// <summary>Checks the tunnel connector against a peer written from the whitepaper, with no network.</summary>
[TestClass]
public sealed class WireGuardConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_AddressInsideAllowedIps_HandshakesAndCarriesTcpThroughTheTunnel()
    {
        TunnelTestPeer peer = new();
        WireGuardConnector connector = new(TunnelTestPeer.Configuration(), peer, new ManualTimeProvider(), RandomNumberGenerator.Create());

        var connecting = connector.ConnectAsync(new ConnectTarget("10.0.0.1", 80, false), CancellationToken.None).AsTask();
        await peer.AcceptHandshakeAsync();
        var (packet, syn) = await peer.AcceptTcpAsync();
        ConnectResult result = await connecting;
        IConnection connection = result.Connection!;
        await connection.WriteAsync(Encoding.ASCII.GetBytes("GET"), CancellationToken.None);
        TcpSegment request = await peer.ReceiveSegmentAsync(TcpFlags.None);
        peer.Reply(packet, new TcpSegment(syn.DestinationPort, syn.SourcePort, TunnelTestPeer.ServerInitialSequenceNumber + 1, request.SequenceNumber + 3, TcpFlags.Ack | TcpFlags.Psh, 65535, null, Encoding.ASCII.GetBytes("OK")));
        byte[] response = new byte[2];
        int read = await connection.ReadAsync(response, CancellationToken.None);
        var closing = connection.DisposeAsync().AsTask();
        TcpSegment fin = await peer.ReceiveSegmentAsync(TcpFlags.Fin);
        peer.Reply(packet, new TcpSegment(syn.DestinationPort, syn.SourcePort, TunnelTestPeer.ServerInitialSequenceNumber + 3, fin.SequenceNumber + 1, TcpFlags.Ack | TcpFlags.Fin, 65535, null, []));
        await closing;

        Assert.AreEqual(new WireGuardEndpoint("192.0.2.1", 51820), peer.OpenedEndpoint);
        Assert.AreEqual(IPAddress.Parse("10.0.0.2"), packet.Source);
        Assert.AreEqual(IPAddress.Parse("10.0.0.1"), packet.Destination);
        Assert.AreEqual((ushort)80, syn.DestinationPort);
        Assert.AreEqual((ushort)(WireGuardConnector.DefaultMtu - 40), syn.MaximumSegmentSize);
        Assert.AreEqual(new IPEndPoint(IPAddress.Parse("10.0.0.1"), 80), connection.RemoteEndPoint);
        Assert.AreEqual("GET", Encoding.ASCII.GetString(request.Payload));
        Assert.AreEqual("OK", Encoding.ASCII.GetString(response, 0, read));
        Assert.IsTrue(peer.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_Ipv6TargetWithMtu_OffersTheMtuLessTheIpv6AndTcpHeaders()
    {
        TunnelTestPeer peer = new();
        WireGuardConnector connector = new(TunnelTestPeer.Configuration(extraInterfaceLine: "MTU = 1280"), peer, new ManualTimeProvider(), RandomNumberGenerator.Create());

        _ = connector.ConnectAsync(new ConnectTarget("[fd00::1]", 443, false), CancellationToken.None).AsTask();
        await peer.AcceptHandshakeAsync();
        var (packet, syn) = await peer.ReceiveSegmentAsync();

        Assert.AreEqual(IPAddress.Parse("fd00::2"), packet.Source);
        Assert.AreEqual((ushort)(1280 - 60), syn.MaximumSegmentSize);
    }

    [TestMethod]
    public async Task ConnectAsync_TlsTargetWithoutTlsProvider_IsNotBuiltIn()
    {
        WireGuardConnector connector = new(TunnelTestPeer.Configuration(), new TunnelTestPeer(), TimeProvider.System, RandomNumberGenerator.Create());

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("10.0.0.1", 443, true), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.NotBuiltIn, result.ExitCode);
    }

    [TestMethod]
    public async Task ConnectAsync_HostName_CannotBeResolvedInsideTheTunnel()
    {
        TunnelTestPeer peer = new();
        WireGuardConnector connector = new(TunnelTestPeer.Configuration(), peer, TimeProvider.System, RandomNumberGenerator.Create());

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("example.com", 80, false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: example.com (--wireguard reaches IP addresses only)", result.ErrorMessage);
        Assert.IsNull(peer.OpenedEndpoint);
    }

    [TestMethod]
    [DataRow("10.0.0.2/32, fd00::2/128", "192.168.1.1")]
    [DataRow("10.0.0.2/32", "fd00::1")]
    public async Task ConnectAsync_NoPeerOrNoSourceAddressForTheTarget_CouldNotConnect(string addresses, string host)
    {
        TunnelTestPeer peer = new();
        WireGuardConnector connector = new(TunnelTestPeer.Configuration(addresses), peer, TimeProvider.System, RandomNumberGenerator.Create());

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget(host, 80, false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual($"Failed to connect to {host} port 80: no WireGuard peer's AllowedIPs and interface Address cover it", result.ErrorMessage);
        Assert.IsNull(peer.OpenedEndpoint);
    }

    [TestMethod]
    public async Task ConnectAsync_EndpointCannotBeOpened_CouldNotConnect()
    {
        TunnelTestPeer peer = new() { OpenFailure = new SocketException((int)SocketError.HostNotFound) };
        WireGuardConnector connector = new(TunnelTestPeer.Configuration(), peer, TimeProvider.System, RandomNumberGenerator.Create());

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("10.0.0.1", 80, false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.StartsWith("Failed to connect to 10.0.0.1 port 80 through the WireGuard tunnel: ", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_PeerNeverResponds_GivesUpAfterRekeyAttemptTimeAndClosesTheSocket()
    {
        TunnelTestPeer peer = new();
        ManualTimeProvider clock = new();
        WireGuardConnector connector = new(TunnelTestPeer.Configuration(), peer, clock, RandomNumberGenerator.Create());

        var connecting = connector.ConnectAsync(new ConnectTarget("10.0.0.1", 80, false), CancellationToken.None).AsTask();
        for (int attempt = 0; attempt < WireGuardTunnelHandshake.MaximumAttempts; attempt++)
        {
            await peer.NextSentAsync();
            clock.Advance(WireGuardTunnelHandshake.RekeyTimeout);
        }

        ConnectResult result = await connecting;

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 10.0.0.1 port 80 through the WireGuard tunnel: the WireGuard handshake had no response after 18 initiations", result.ErrorMessage);
        Assert.IsTrue(peer.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_ServerResetsTheSyn_CouldNotConnect()
    {
        TunnelTestPeer peer = new();
        WireGuardConnector connector = new(TunnelTestPeer.Configuration(), peer, new ManualTimeProvider(), RandomNumberGenerator.Create());

        var connecting = connector.ConnectAsync(new ConnectTarget("10.0.0.1", 80, false), CancellationToken.None).AsTask();
        await peer.AcceptHandshakeAsync();
        var (packet, syn) = await peer.ReceiveSegmentAsync();
        peer.Reply(packet, new TcpSegment(syn.DestinationPort, syn.SourcePort, 0, syn.SequenceNumber + 1, TcpFlags.Rst | TcpFlags.Ack, 0, null, []));
        ConnectResult result = await connecting;

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(peer.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_CancelledDuringTheHandshake_ThrowsAndClosesTheSocket()
    {
        TunnelTestPeer peer = new();
        WireGuardConnector connector = new(TunnelTestPeer.Configuration(), peer, new ManualTimeProvider(), RandomNumberGenerator.Create());
        using CancellationTokenSource cancellation = new();

        var connecting = connector.ConnectAsync(new ConnectTarget("10.0.0.1", 80, false), cancellation.Token).AsTask();
        await peer.NextSentAsync();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => connecting);
        Assert.IsTrue(peer.IsDisposed);
    }

    [TestMethod]
    [DataRow("example.com", "example.com")]
    [DataRow(null, "10.0.0.1")]
    public async Task ConnectAsync_TlsTarget_RunsCurlsHandshakeOverTheTunnelWithCurlsAlpn(string? tlsServerName, string expectedTargetHost)
    {
        TunnelTestPeer peer = new();
        RecordingTlsProvider tls = new();
        WireGuardConnector connector = new(TunnelTestPeer.Configuration(), peer, new ManualTimeProvider(), RandomNumberGenerator.Create(), tls, ["h2", "http/1.1"]);

        var connecting = connector.ConnectAsync(new ConnectTarget("10.0.0.1", 443, true) { PoolScheme = "https", TlsServerName = tlsServerName }, CancellationToken.None).AsTask();
        await peer.AcceptHandshakeAsync();
        await peer.AcceptTcpAsync();
        ConnectResult result = await connecting;

        Assert.IsInstanceOfType<WireGuardConnection>(result.Connection);
        Assert.AreEqual(expectedTargetHost, tls.TargetHost);
        CollectionAssert.AreEqual(new[] { "h2", "http/1.1" }, tls.ApplicationProtocols!.ToArray());
    }

    [TestMethod]
    public void ApplicationProtocolsFor_EachScheme_OffersCurlsListForHttpsAloneUnlessTheTargetNamesOne()
    {
        WireGuardConnector connector = new(TunnelTestPeer.Configuration(), new TunnelTestPeer(), TimeProvider.System, RandomNumberGenerator.Create());

        CollectionAssert.AreEqual(new[] { "http/1.1" }, connector.ApplicationProtocolsFor(new ConnectTarget("10.0.0.1", 443, true) { PoolScheme = "https" }).ToArray());
        CollectionAssert.AreEqual(new[] { "h2" }, connector.ApplicationProtocolsFor(new ConnectTarget("10.0.0.1", 443, true) { PoolScheme = "HTTPS", ApplicationProtocols = ["h2"] }).ToArray());
        Assert.IsEmpty(connector.ApplicationProtocolsFor(new ConnectTarget("10.0.0.1", 993, true) { PoolScheme = "imaps" }));
    }

    [TestMethod]
    public async Task ConnectAsync_NullTarget_Throws()
    {
        WireGuardConnector connector = new(TunnelTestPeer.Configuration(), new TunnelTestPeer(), TimeProvider.System, RandomNumberGenerator.Create());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => connector.ConnectAsync(null!, CancellationToken.None).AsTask());
    }

    private sealed class RecordingTlsProvider : ITlsProvider
    {
        public string? TargetHost { get; private set; }

        public IReadOnlyList<string>? ApplicationProtocols { get; private set; }

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
            throw new AssertFailedException("The connector should hand its ALPN list.");

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, ITransferEvents events, IReadOnlyList<string> applicationProtocols, CancellationToken cancellationToken)
        {
            TargetHost = targetHost;
            ApplicationProtocols = applicationProtocols;
            return ValueTask.FromResult(ConnectResult.Connected(plaintext));
        }
    }
}
