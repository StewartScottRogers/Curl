using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector" /> through a SOCKS proxy reached over a Unix domain socket
/// (<see cref="ProxyEndpoint.UnixSocketPath" />), as curl reads <c>socks5h://localhost/path</c> and
/// <c>--socks5 localhost/path</c> since 7.84.0 (upstream tests 1467, 1468 and 1470; BL-2955).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_ThroughSocks5hOverAUnixSocket_DialsTheSocketAndSendsTheTargetName()
    {
        var (result, proxyConnection, dialer) = await ConnectThroughSocksOverUnixSocketAsync(ProxyKind.Socks5Hostname, [.. Socks5NoAuthentication, .. Socks5Succeeded]);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(proxyConnection, result.Connection);
        Assert.AreEqual("/some/path", dialer.DialedUnixSockets.Single().Path);
        Assert.IsFalse(dialer.DialedUnixSockets.Single().IsAbstract);
        Assert.IsEmpty(dialer.DialedEndPoints);
        CollectionAssert.AreEqual(
            (byte[])[0x05, 0x02, 0x00, 0x01, 0x05, 0x01, 0x00, 0x03, 0x0E, .. "target.example"u8, 0x1F, 0x90],
            proxyConnection.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks5OverAUnixSocket_ResolvesTheTargetLocallyAndSendsItsAddress()
    {
        var (result, proxyConnection, dialer) = await ConnectThroughSocksOverUnixSocketAsync(ProxyKind.Socks5, [.. Socks5NoAuthentication, .. Socks5Succeeded]);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("/some/path", dialer.DialedUnixSockets.Single().Path);
        Assert.IsEmpty(dialer.DialedEndPoints);
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x01, 0x00, 0x01, 10, 1, 2, 3, 0x1F, 0x90 }, proxyConnection.Written[4..]);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocksOverAUnixSocket_NamesTheSocketPathOnPortZero()
    {
        var events = new RecordingTransferEvents();

        await ConnectThroughSocksOverUnixSocketAsync(ProxyKind.Socks5Hostname, [.. Socks5NoAuthentication, .. Socks5Succeeded], events: events);

        CollectionAssert.Contains(events.Info, "  Trying /some/path:0...");
        CollectionAssert.Contains(events.Info, "Opened SOCKS connection from  port 0 to target.example port 8080 (via /some/path port 0)");
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocksOverAUnixSocketThatRefuses_FailsWithCouldntConnect()
    {
        var (result, proxyConnection, _) = await ConnectThroughSocksOverUnixSocketAsync(ProxyKind.Socks5Hostname, Socks5NoAuthentication, refuses: true);

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsEmpty(proxyConnection.Written);
    }

    // Connects to target.example:8080 through a SOCKS proxy at localhost over the Unix socket
    // /some/path that answers with proxyReply; --resolve answers the target for a local resolve.
    private async Task<(ConnectResult Result, ScriptedConnection ProxyConnection, FakeTcpDialer Dialer)> ConnectThroughSocksOverUnixSocketAsync(
        ProxyKind kind,
        byte[] proxyReply,
        bool refuses = false,
        RecordingTransferEvents? events = null)
    {
        var proxyConnection = new ScriptedConnection(proxyReply);
        var dialer = refuses ? new FakeTcpDialer() : new FakeTcpDialer { UnixSocketDialOutcome = _ => proxyConnection };
        var connector = new TcpConnector(
            new FakeDnsResolver(),
            dialer,
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(["target.example:8080:10.1.2.3"]));

        Diagnostics.Arrange("proxy", $"{kind} at localhost over the Unix socket /some/path");
        var proxy = new ProxyEndpoint(kind, "localhost", 1080, null) { UnixSocketPath = "/some/path" };
        var result = await ConnectLoggedAsync(
            connector,
            new ConnectTarget("target.example", 8080, UseTls: false) { Proxy = proxy, Events = events ?? new RecordingTransferEvents() });
        Diagnostics.Bytes("sent to proxy", [.. proxyConnection.Written]);
        return (result, proxyConnection, dialer);
    }
}
