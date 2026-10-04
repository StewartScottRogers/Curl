using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <see cref="TcpConnector" /> refusing a <c>.onion</c> name before any look-up, as curl 8.21.0's
/// <c>Curl_resolv</c> does under RFC 7686 (measured, BL-1394).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    [DataRow("example.onion", 80, DisplayName = "curl -sv http://example.onion/")]
    [DataRow("example.onion.", 80, DisplayName = "curl -v http://example.onion./x")]
    [DataRow("a.ONION", 21, DisplayName = "curl -sv ftp://a.ONION/")]
    public async Task ConnectAsync_ToAnOnionName_FailsWithExit6BeforeAnyLookUp(string host, int port)
    {
        var resolver = new FakeDnsResolver(Loopback);
        var dialer = new FakeTcpDialer();
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(resolver, dialer, new FakeTlsProvider(), new ManualTimeProvider());

        var result = await connector.ConnectAsync(new ConnectTarget(host, port, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Not resolving .onion address (RFC 7686)", result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[] { "Not resolving .onion address (RFC 7686)", $"Could not resolve: {host}:{port}", $"Could not resolve: {host}" },
            events.Info);
        Assert.IsEmpty(resolver.ResolvedHosts);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnOnionNameWithAResolveEntry_RefusesAfterAddingTheEntryAndDialsNothing()
    {
        // curl -sv --resolve x.onion:80:127.0.0.1 http://x.onion:80/ -> Added ... to DNS cache, then the refusal; exit 6.
        var resolver = new FakeDnsResolver(Loopback);
        var dialer = new FakeTcpDialer();
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(resolver, dialer, new FakeTlsProvider(), new ManualTimeProvider(), resolveOverrides: ResolveOverrides.Parse(["x.onion:80:127.0.0.1"]));

        var result = await connector.ConnectAsync(new ConnectTarget("x.onion", 80, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "Added x.onion:80:127.0.0.1 to DNS cache", "Not resolving .onion address (RFC 7686)", "Could not resolve: x.onion:80", "Could not resolve: x.onion" },
            events.Info);
        Assert.IsEmpty(resolver.ResolvedHosts);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    [TestMethod]
    [DataRow("onion")]
    [DataRow(".onion")]
    [DataRow("x.onion.example")]
    public async Task ConnectAsync_ToANameThatIsNotAnOnionAddress_LooksItUp(string host)
    {
        var resolver = new FakeDnsResolver(Loopback);
        var connector = new TcpConnector(resolver, new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider());

        var result = await connector.ConnectAsync(new ConnectTarget(host, 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { host }, resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnOnionNameThroughAnHttpProxy_LeavesTheNameToTheProxy()
    {
        // curl -sv --proxy http://127.0.0.1:1 http://example.onion/ -> no refusal; exit 7 on the proxy connect.
        var resolver = new FakeDnsResolver(Loopback);
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = new TcpConnector(resolver, dialer, new FakeTlsProvider(), new ManualTimeProvider());
        var target = new ConnectTarget("example.onion", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 1, null) };

        var result = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        CollectionAssert.DoesNotContain(resolver.ResolvedHosts, "example.onion");
        Assert.HasCount(1, dialer.DialedEndPoints);
    }
}
