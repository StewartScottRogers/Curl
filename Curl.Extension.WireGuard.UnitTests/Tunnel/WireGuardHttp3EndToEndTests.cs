using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Http3Server;
using Curl.Console;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.IpStack;
using Curl.Extension.WireGuard.IpStack.Udp;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.UnitTests.Tunnel;

/// <summary>
/// Runs <c>curl --wireguard wg0.conf --http3-only</c> end to end with no network: curl's QUIC datagrams travel sealed
/// through the tunnel to a fake peer, which relays each UDP payload to an in-memory HTTP/3 server and seals its replies back.
/// </summary>
[TestClass]
public sealed class WireGuardHttp3EndToEndTests
{
    private const string Body = "through the tunnel\n";

    [TestMethod]
    public async Task RunAsync_Http3OnlyThroughWireGuard_CarriesTheQuicDatagramsThroughTheTunnelToThePeer()
    {
        TunnelTestPeer peer = new();
        using X509Certificate2 certificate = CreateRsaCertificate();
        IDatagramConnector http3Server = UpstreamHttp3ServerFactory.Create(new RespondingBackendConnector(), Http3ServerTlsSettings.FromRsaCertificate(certificate, []));
        using RandomNumberGenerator random = RandomNumberGenerator.Create();
        using CancellationTokenSource relayStop = new();
        Task<int> relaying = RelayThroughThePeerAsync(peer, http3Server, relayStop.Token);
        MemoryStream standardOutput = new();
        MemoryStream standardError = new();

        int exitCode = await CurlComposition.CreateRunner(
                standardOutput,
                standardError,
                new MemoryStream(),
                new RefusingTcpDialer(),
                new LiteralOnlyDnsResolver(),
                new RefusingDatagramConnector(),
                parsesAsWindowsBuild: false,
                parsesAsQuicBuild: true,
                extensionConnectionHookActivators: [values => WireGuardExtension.Activate(values, _ => TunnelTestPeer.ConfigurationText(), peer, TimeProvider.System, random)])
            .RunAsync(["-s", "-S", "--wireguard", "wg0.conf", "--http3-only", "-k", "https://10.0.0.1/"]);
        await relayStop.CancelAsync();
        int relayedDatagrams = await relaying;

        Assert.AreEqual(0, exitCode, Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.AreEqual(Body, Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(new WireGuardEndpoint("192.0.2.1", 51820), peer.OpenedEndpoint);
        Assert.IsGreaterThan(0, relayedDatagrams);
    }

    /// <summary>
    /// Plays the peer: answers the handshake, then opens each transport data message, relays the UDP payload inside it
    /// to <paramref name="http3Server" />, and seals every reply back as UDP from 10.0.0.1:443, until <paramref name="stop" />.
    /// </summary>
    /// <returns>The number of UDP datagrams relayed to the server.</returns>
    private static async Task<int> RelayThroughThePeerAsync(TunnelTestPeer peer, IDatagramConnector http3Server, CancellationToken stop)
    {
        await peer.AcceptHandshakeAsync();
        IDatagramChannel server = (await http3Server.OpenAsync("10.0.0.1", 443, stop)).Channel!;
        TaskCompletionSource<IPEndPoint> curlEndPoint = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task replying = ReplyThroughThePeerAsync(peer, server, curlEndPoint.Task, stop);
        int relayed = 0;
        try
        {
            while (true)
            {
                byte[]? opened = peer.Responder.OpenTransport(await peer.NextSentAsync(stop));
                if (opened is { Length: > 0 } && IpPacketCodec.TryParse(opened, out IpPacket? packet) && UdpDatagramCodec.TryParse(packet!, out UdpDatagram? datagram))
                {
                    Assert.AreEqual(IPAddress.Parse("10.0.0.1"), packet!.Destination);
                    Assert.AreEqual((ushort)443, datagram!.DestinationPort);
                    curlEndPoint.TrySetResult(new IPEndPoint(packet.Source, datagram.SourcePort));
                    await server.SendAsync(datagram.Payload, server.ServerEndPoint, stop);
                    relayed++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            await IgnoringCancellationAsync(replying);
            await server.DisposeAsync();
            return relayed;
        }
    }

    private static async Task ReplyThroughThePeerAsync(TunnelTestPeer peer, IDatagramChannel server, Task<IPEndPoint> curlEndPoint, CancellationToken stop)
    {
        IPEndPoint curl = await curlEndPoint.WaitAsync(stop);
        IPEndPoint quicServer = new(IPAddress.Parse("10.0.0.1"), 443);
        byte[] buffer = new byte[65535];
        while (true)
        {
            DatagramReceived received = await server.ReceiveAsync(buffer, stop);
            byte[] udp = UdpDatagramCodec.Build(quicServer, curl, buffer.AsSpan(0, received.Length));
            peer.Deliver(peer.SealToInitiator(IpPacketCodec.Build(new IpPacket(quicServer.Address, curl.Address, UdpDatagramCodec.UdpProtocol, udp))));
        }
    }

    private static async Task IgnoringCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static X509Certificate2 CreateRsaCertificate()
    {
        using RSA key = RSA.Create(2048);
        return new CertificateRequest("CN=10.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    /// <summary>The origin behind the HTTP/3 server: answers every request with <see cref="Body" />.</summary>
    private sealed class RespondingBackendConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(new RespondingConnection()));
    }

    private sealed class RespondingConnection : IConnection
    {
        private readonly byte[] reply = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {Body.Length}\r\nConnection: close\r\n\r\n{Body}");

        private int replied;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int taken = Math.Min(buffer.Length, reply.Length - replied);
            reply.AsMemory(replied, taken).CopyTo(buffer);
            replied += taken;
            return ValueTask.FromResult(taken);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Curl's own TCP dialer: under <c>--wireguard</c> nothing dials outside the tunnel.</summary>
    private sealed class RefusingTcpDialer : ITcpDialer
    {
        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken) =>
            throw new SocketException((int)SocketError.ConnectionRefused);

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No Unix socket is dialled.");
    }

    /// <summary>Curl's own resolver: answers an address literal with itself, and refuses any name, which would resolve outside the tunnel.</summary>
    private sealed class LiteralOnlyDnsResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            IPAddress.TryParse(host, out IPAddress? address)
                ? ValueTask.FromResult<IReadOnlyList<IPAddress>>([address])
                : throw new InvalidOperationException($"{host} resolved outside the WireGuard tunnel.");
    }

    /// <summary>Curl's own datagram connector: a QUIC datagram opened here would leave outside the tunnel.</summary>
    private sealed class RefusingDatagramConnector : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            throw new InvalidOperationException($"QUIC to {host}:{port} opened outside the WireGuard tunnel.");
    }
}
