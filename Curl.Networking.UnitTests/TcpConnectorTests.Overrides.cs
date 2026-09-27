using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector" /> with <c>--resolve</c> and <c>--connect-to</c>
/// overrides: what is resolved, what is dialled, what TLS verifies, and curl 8.21.0's
/// messages (measured; the commands are in BL-214's Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_WithAResolveEntryForTheHostAndPort_DialsItsAddressWithoutTheResolver()
    {
        // curl --resolve a:80:127.0.0.1 http://a:80/ -> Host a:80 was resolved. Trying 127.0.0.1:80
        var resolver = new FakeDnsResolver(IPAddress.Parse("192.0.2.99"));
        var connection = new FakeConnection();
        var dialer = new FakeTcpDialer { DialOutcome = _ => connection };
        var connector = new TcpConnector(
            resolver, dialer, new FakeTlsProvider(), new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(["a:80:127.0.0.1"]));

        var result = await connector.ConnectAsync(new ConnectTarget("a", 80, UseTls: false), CancellationToken.None);

        Assert.AreSame(connection, result.Connection);
        Assert.IsEmpty(resolver.ResolvedHosts);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(Loopback, 80) }, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAResolveEntryForAnotherPort_UsesTheResolver()
    {
        var resolver = new FakeDnsResolver(IPAddress.Parse("192.0.2.99"));
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(
            resolver, dialer, new FakeTlsProvider(), new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(["a:81:127.0.0.1"]));

        await connector.ConnectAsync(new ConnectTarget("a", 80, UseTls: false), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "a" }, resolver.ResolvedHosts);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Parse("192.0.2.99"), 80) }, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAResolveEntryThatDoesNotParse_FailsWithExit49BeforeResolving()
    {
        // curl --resolve garbage http://a/ -> curl: (49) Could not parse CURLOPT_RESOLVE entry 'garbage'
        var resolver = new FakeDnsResolver(Loopback);
        var dialer = new FakeTcpDialer();
        var connector = new TcpConnector(
            resolver, dialer, new FakeTlsProvider(), new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(["garbage"]));

        var result = await connector.ConnectAsync(new ConnectTarget("a", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual("Could not parse CURLOPT_RESOLVE entry 'garbage'", result.ErrorMessage);
        Assert.IsEmpty(resolver.ResolvedHosts);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAConnectToMapping_DialsTheMappedHostAndPortButVerifiesTheUrlsHost()
    {
        // curl --connect-to a:443:b.example:8443 https://a/ : b.example is resolved and dialled on 8443; TLS verifies a
        var resolver = new FakeDnsResolver(Loopback);
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var tlsProvider = new FakeTlsProvider();
        var connector = new TcpConnector(
            resolver, dialer, tlsProvider, new ManualTimeProvider(),
            connectToMappings: new ConnectToMappings(["a:443:b.example:8443"]));

        var result = await connector.ConnectAsync(new ConnectTarget("a", 443, UseTls: true), CancellationToken.None);

        Assert.AreSame(tlsProvider.SecuredConnection, result.Connection);
        CollectionAssert.AreEqual(new[] { "b.example" }, resolver.ResolvedHosts);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(Loopback, 8443) }, dialer.DialedEndPoints);
        Assert.AreEqual("a", tlsProvider.ReceivedTargetHost);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAConnectToMappingAndAResolveEntryForTheMappedHostAndPort_DialsTheEntrysAddress()
    {
        // curl --connect-to a:80::9 --resolve a:9:127.0.0.5 http://a/ -> Host a:9 was resolved. Trying 127.0.0.5:9
        var resolver = new FakeDnsResolver(Loopback);
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(
            resolver, dialer, new FakeTlsProvider(), new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(["a:9:127.0.0.5"]),
            connectToMappings: new ConnectToMappings(["a:80::9"]));

        await connector.ConnectAsync(new ConnectTarget("a", 80, UseTls: false), CancellationToken.None);

        Assert.IsEmpty(resolver.ResolvedHosts);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Parse("127.0.0.5"), 9) }, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheMappedDestinationRefuses_NamesItAfterVia()
    {
        // curl --connect-to a:80:127.0.0.8:0 http://a/ ->
        // curl: (7) Failed to connect to a:80 via 127.0.0.8:0 after 0 ms: Could not connect to server
        var connector = new TcpConnector(
            new FakeDnsResolver(IPAddress.Parse("127.0.0.8")), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider(),
            connectToMappings: new ConnectToMappings(["a:80:127.0.0.8:0"]));

        var result = await connector.ConnectAsync(new ConnectTarget("a", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to a:80 via 127.0.0.8:0 after 0 ms: Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheMappedHostDoesNotResolve_NamesTheMappedHost()
    {
        // curl --connect-to a:80:nosuch.invalid:81 http://a/ -> curl: (6) Could not resolve host: nosuch.invalid
        var connector = new TcpConnector(
            new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider(),
            connectToMappings: new ConnectToMappings(["a:80:nosuch.invalid:81"]));

        var result = await connector.ConnectAsync(new ConnectTarget("a", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: nosuch.invalid", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheMatchingConnectToMappingDoesNotParse_FailsWithExit49BeforeResolving()
    {
        // curl --connect-to a:80:b:x http://a/ -> curl: (49) No valid port number in 'b:x'
        var resolver = new FakeDnsResolver(Loopback);
        var connector = new TcpConnector(
            resolver, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider(),
            connectToMappings: new ConnectToMappings(["a:80:b:x"]));

        var result = await connector.ConnectAsync(new ConnectTarget("a", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual("No valid port number in 'b:x'", result.ErrorMessage);
        Assert.IsEmpty(resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAProxy_ResolvesTheProxyFromAResolveEntryAndTunnelsToTheMappedHost()
    {
        // curl -p -x http://p.example:38123 --resolve p.example:38123:127.0.0.1 --connect-to a:80:b.example:81 http://a/ ->
        // CONNECT b.example:81 HTTP/1.1\r\nHost: b.example:81\r\n...
        var resolver = new FakeDnsResolver(IPAddress.Parse("192.0.2.99"));
        var proxyConnection = new ScriptedConnection(Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
        var dialer = new FakeTcpDialer { DialOutcome = _ => proxyConnection };
        var connector = new TcpConnector(
            resolver, dialer, new FakeTlsProvider(), new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(["p.example:38123:127.0.0.1"]),
            connectToMappings: new ConnectToMappings(["a:80:b.example:81"]));
        var target = new ConnectTarget("a", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "p.example", 38123, null) };

        var result = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreSame(proxyConnection, result.Connection);
        Assert.IsEmpty(resolver.ResolvedHosts);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(Loopback, 38123) }, dialer.DialedEndPoints);
        Assert.AreEqual(
            "CONNECT b.example:81 HTTP/1.1\r\nHost: b.example:81\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString([.. proxyConnection.Written]));
    }

    [TestMethod]
    public async Task ConnectAsync_WithoutOverrides_ResolvesAndDialsTheUrlsHostAndPort()
    {
        var resolver = new FakeDnsResolver(Loopback);
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(resolver, dialer, new FakeTlsProvider(), new ManualTimeProvider(), null, null, null);

        await connector.ConnectAsync(new ConnectTarget("a", 80, UseTls: false), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "a" }, resolver.ResolvedHosts);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(Loopback, 80) }, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAMappedDialRefuses_TriesTheNextAddress()
    {
        var first = IPAddress.Parse("127.0.0.3");
        var connection = new FakeConnection();
        var dialer = new FakeTcpDialer
        {
            DialOutcome = endPoint => endPoint.Address.Equals(first) ? throw new SocketException((int)SocketError.ConnectionRefused) : connection,
        };
        var connector = new TcpConnector(
            new FakeDnsResolver(), dialer, new FakeTlsProvider(), new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(["b:9:127.0.0.3,127.0.0.4"]),
            connectToMappings: new ConnectToMappings(["a:80:b:9"]));

        var result = await connector.ConnectAsync(new ConnectTarget("a", 80, UseTls: false), CancellationToken.None);

        Assert.AreSame(connection, result.Connection);
        Assert.HasCount(2, dialer.DialedEndPoints);
    }
}
