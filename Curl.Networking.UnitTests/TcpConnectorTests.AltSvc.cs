using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector" /> with a <see cref="ConnectTarget.AltSvcRoute" />: the
/// alternative is dialled as a <c>--connect-to</c> destination is, after curl 8.21.0's
/// <c>Alt-svc connecting</c> line (measured with <c>--alt-svc</c>; the commands are in BL-623's Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private static readonly AltSvcRoute RouteToAlternative = new("h1", new AltSvcAlternative("h1", "localhost", 18443));

    [TestMethod]
    public async Task ConnectAsync_WithAnAltSvcRoute_DialsTheAlternativeButVerifiesTheOriginsHost()
    {
        // curl -k --alt-svc cache.txt https://localhost:18499/ with "h1 localhost 18499 h1 localhost 18443 ..."
        var resolver = new FakeDnsResolver(Loopback);
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var tlsProvider = new FakeTlsProvider();
        var connector = new TcpConnector(resolver, dialer, tlsProvider, new ManualTimeProvider());

        var result = await connector.ConnectAsync(
            new ConnectTarget("localhost", 18499, UseTls: true) { AltSvcRoute = RouteToAlternative },
            CancellationToken.None);

        Assert.AreSame(tlsProvider.SecuredConnection, result.Connection);
        CollectionAssert.AreEqual(new[] { "localhost" }, resolver.ResolvedHosts);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(Loopback, 18443) }, dialer.DialedEndPoints);
        Assert.AreEqual("localhost", tlsProvider.ReceivedTargetHost);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAnAltSvcRoute_ReportsAltSvcConnectingBeforeTheHostWasResolved()
    {
        // * Alt-svc connecting from [h1]localhost:18499 to [h1]localhost:18443
        // * Host localhost:18443 was resolved.
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider());

        await connector.ConnectAsync(
            new ConnectTarget("localhost", 18499, UseTls: true) { AltSvcRoute = RouteToAlternative, Events = events },
            CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "Alt-svc connecting from [h1]localhost:18499 to [h1]localhost:18443", "Host localhost:18443 was resolved." },
            events.Info.Take(2).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheAlternativeRefuses_FailsWithExit7NamingItAfterVia()
    {
        // curl: (7) Failed to connect to localhost:18443 via localhost:18498 after 2234 ms: Could not connect to server
        var route = new AltSvcRoute("h1", new AltSvcAlternative("h1", "localhost", 18498));
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());

        var result = await connector.ConnectAsync(
            new ConnectTarget("localhost", 18443, UseTls: true) { AltSvcRoute = route },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to localhost:18443 via localhost:18498 after 0 ms: Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAnAltSvcRouteAndAMatchingConnectToMapping_DialsTheMappingAndReportsNoAltSvcLine()
    {
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider(),
            connectToMappings: new ConnectToMappings(["localhost:18499:127.0.0.1:9"]));

        await connector.ConnectAsync(
            new ConnectTarget("localhost", 18499, UseTls: true) { AltSvcRoute = RouteToAlternative, Events = events },
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { new IPEndPoint(Loopback, 9) }, dialer.DialedEndPoints);
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("Alt-svc", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ConnectAsync_WithAnAltSvcRouteAndAMatchingConnectToMappingThatDoesNotParse_FailsWithExit49()
    {
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider(),
            connectToMappings: new ConnectToMappings(["localhost:18499:b:x"]));

        var result = await connector.ConnectAsync(
            new ConnectTarget("localhost", 18499, UseTls: true) { AltSvcRoute = RouteToAlternative },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual("No valid port number in 'b:x'", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAnAltSvcRouteAndAConnectToMappingForAnotherHost_DialsTheAlternative()
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider(),
            connectToMappings: new ConnectToMappings(["other:18499:127.0.0.1:9"]));

        await connector.ConnectAsync(
            new ConnectTarget("localhost", 18499, UseTls: true) { AltSvcRoute = RouteToAlternative },
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { new IPEndPoint(Loopback, 18443) }, dialer.DialedEndPoints);
    }
}
