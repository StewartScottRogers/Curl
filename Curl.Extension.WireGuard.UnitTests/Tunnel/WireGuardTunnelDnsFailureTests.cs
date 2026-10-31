using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Curl.Extension.WireGuard.Tunnel;
using Curl.Extension.WireGuard.UnitTests.IpStack;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.UnitTests.Tunnel;

/// <summary>Checks that a tunnel that cannot reach its DNS server ends an HTTP/3 transfer with exit 7, as the TCP path does, and a name with no answer with exit 6 (BL-4424).</summary>
[TestClass]
public sealed class WireGuardTunnelDnsFailureTests
{
    private const string DnsLine = "DNS = 10.0.0.53";

    private static WireGuardConnector ConnectorWithQuic(TunnelTestPeer peer, out IDnsResolver resolver)
    {
        var configuration = TunnelTestPeer.Configuration(extraInterfaceLine: DnsLine);
        WireGuardResolverHook hook = new(configuration, peer, new ManualTimeProvider(), RandomNumberGenerator.Create());
        resolver = hook.ResolverOver(new LiteralOnlyResolver());
        return new WireGuardConnector(configuration, peer, new ManualTimeProvider(), RandomNumberGenerator.Create(), quicConnector: new ResolvingQuicConnector(resolver));
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_DnsServerCannotBeReached_CouldNotConnectLikeTheTcpPath()
    {
        TunnelTestPeer peer = new() { OpenFailure = new SocketException((int)SocketError.NetworkUnreachable) };
        WireGuardConnector connector = ConnectorWithQuic(peer, out _);

        MultiplexedConnectResult result = await connector.ConnectMultiplexedAsync(new ConnectTarget("example.com", 443, true), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.StartsWith("Failed to connect to 10.0.0.53 port 53 through the WireGuard tunnel: ", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_DnsServerCannotBeReached_CouldNotConnectLikeTheTcpPath()
    {
        TunnelTestPeer peer = new() { OpenFailure = new SocketException((int)SocketError.NetworkUnreachable) };
        WireGuardConnector connector = ConnectorWithQuic(peer, out _);

        ConnectResult result = await connector.ConnectMultiplexedSessionAsync(new ConnectTarget("example.com", 443, true), _ => throw new AssertFailedException("no session"), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.StartsWith("Failed to connect to 10.0.0.53 port 53 through the WireGuard tunnel: ", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ResolveAsync_DnsServerCannotBeReached_ThrowsTheConnectFailure()
    {
        TunnelTestPeer peer = new() { OpenFailure = new SocketException((int)SocketError.NetworkUnreachable) };
        ConnectorWithQuic(peer, out IDnsResolver resolver);

        WireGuardTunnelDnsFailureException exception = await Assert.ThrowsExactlyAsync<WireGuardTunnelDnsFailureException>(
            async () => await resolver.ResolveAsync("example.com", CancellationToken.None));

        Assert.AreEqual(CurlExitCode.CouldntConnect, exception.Failure.ExitCode);
    }

    [TestMethod]
    public async Task ResolveAsync_NameWithNoAnswer_GivesNoAddressSoCurlSaysCouldNotResolve()
    {
        TunnelTestPeer peer = new();
        ConnectorWithQuic(peer, out IDnsResolver resolver);

        Task<IReadOnlyList<IPAddress>> resolving = resolver.ResolveAsync("example.com", CancellationToken.None).AsTask();
        var (dnsPacket, dnsSyn, query) = await WireGuardConnectorTests.AcceptDnsQueryAsync(peer);
        await WireGuardConnectorTests.AnswerAndCloseAsync(peer, dnsPacket, dnsSyn, query);

        Assert.IsEmpty(await resolving);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ConnectorSucceeds_GivesItsResult()
    {
        TunnelTestPeer peer = new();
        WireGuardConnector connector = ConnectorWithQuic(peer, out _);

        MultiplexedConnectResult result = await connector.ConnectMultiplexedAsync(new ConnectTarget("10.0.0.1", 443, true), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("stub dialled", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(typeof(SocketException))]
    [DataRow(typeof(TimeoutException))]
    [DataRow(typeof(IOException))]
    public async Task ExchangeAsync_TunnelFailureWhileReading_IsTheTunnelFailure(Type failure)
    {
        Exception thrown = failure == typeof(SocketException) ? new SocketException((int)SocketError.ConnectionReset) : (Exception)Activator.CreateInstance(failure)!;
        ConnectTarget dnsTarget = new("10.0.0.53", 53, false);

        TunnelResolution resolution = await WireGuardConnector.ExchangeAsync(new FailingConnection(thrown), dnsTarget, [0], 1, WireGuardDnsMessage.AddressType, CancellationToken.None);

        Assert.IsNull(resolution.Address);
        Assert.AreEqual(CurlExitCode.CouldntConnect, resolution.Failure!.ExitCode);
    }

    private sealed class LiteralOnlyResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the system resolver must not be asked");
    }

    private sealed class ResolvingQuicConnector(IDnsResolver resolver) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("TCP is not used");

        public async ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            await resolver.ResolveAsync(target.Host, cancellationToken);
            return MultiplexedConnectResult.Failed(CurlExitCode.CouldntConnect, "stub dialled");
        }

        public async ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken)
        {
            await resolver.ResolveAsync(target.Host, cancellationToken);
            return ConnectResult.Failed(CurlExitCode.CouldntConnect, "stub dialled");
        }
    }

    private sealed class FailingConnection(Exception failure) : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromException<int>(failure);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
