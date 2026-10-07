using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives curl 8.21.0's DNS cache in <see cref="TcpConnector" />: the
/// <c>Hostname H was found in DNS cache</c> line it reports before connecting to a host and
/// port it already resolved on the command line, or one a <c>--resolve</c> entry answers
/// (measured 2026-09-27, BL-481).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private const string TryingLoopback = "  Trying 127.0.0.1:47181...";

    [TestMethod]
    public async Task ConnectAsync_ToAHostAndPortAlreadyConnectedTo_ReportsFoundInDnsCacheBeforeTryingOnlyTheSecondTime()
    {
        // curl -s -v http://127.0.0.1:47181/a http://127.0.0.1:47181/b, each closed ->
        // *   Trying 127.0.0.1:47181... ... * shutting down connection #0
        // * Hostname 127.0.0.1 was found in DNS cache
        // *   Trying 127.0.0.1:47181...
        var resolver = new FakeDnsResolver(Loopback);
        var connector = CreateConnector(resolver, new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider());
        var first = new RecordingTransferEvents();
        var second = new RecordingTransferEvents();

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 47181, UseTls: false) { Events = first });
        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 47181, UseTls: false) { Events = second });

        Diagnostics.Assert("first.Info", string.Join(" | ", new[] { TryingLoopback }), string.Join(" | ", first.Info));
        CollectionAssert.AreEqual(new[] { TryingLoopback }, first.Info);
        CollectionAssert.AreEqual(new[] { "Hostname 127.0.0.1 was found in DNS cache", TryingLoopback }, second.Info);
        CollectionAssert.AreEqual(new[] { "127.0.0.1" }, resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ConnectAsync_ToANameAlreadyResolved_ReportsFoundInDnsCacheAndTheResolvedLinesAndDialsTheCachedAddresses()
    {
        // curl -s -v http://localhost:47181/a http://localhost:47181/b ->
        // * Host localhost:47181 was resolved. / * IPv6: ::1 / * IPv4: 127.0.0.1 before the first
        // Trying, and * Hostname localhost was found in DNS cache before the same three the second time.
        var resolver = new FakeDnsResolver(IPAddress.IPv6Loopback, Loopback);
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = CreateConnector(resolver, dialer, new FakeTlsProvider());
        var first = new RecordingTransferEvents();
        var second = new RecordingTransferEvents();

        await ConnectLoggedAsync(connector, new ConnectTarget("localhost", 47181, UseTls: false) { Events = first });
        await ConnectLoggedAsync(connector, new ConnectTarget("localhost", 47181, UseTls: false) { Events = second });

        string[] resolvedLines = ["Host localhost:47181 was resolved.", "IPv6: ::1", "IPv4: 127.0.0.1", "  Trying [::1]:47181..."];
        Diagnostics.Assert("first.Info", string.Join(" | ", resolvedLines), string.Join(" | ", first.Info));
        CollectionAssert.AreEqual(resolvedLines, first.Info);
        CollectionAssert.AreEqual(new[] { "Hostname localhost was found in DNS cache" }.Concat(resolvedLines).ToArray(), second.Info);
        CollectionAssert.AreEqual(new[] { "localhost" }, resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnotherHostOrPort_ResolvesItWithoutTheFoundInDnsCacheLine()
    {
        // curl -s -v http://127.0.0.1:47181/a http://localhost:47181/b -> no cache line for localhost;
        // curl -s -v http://127.0.0.1:1/a http://127.0.0.1:47181/b -> none for the second port either.
        var resolver = new FakeDnsResolver(Loopback);
        var connector = CreateConnector(resolver, new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider());
        var otherHost = new RecordingTransferEvents();
        var otherPort = new RecordingTransferEvents();

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 47181, UseTls: false));
        await ConnectLoggedAsync(connector, new ConnectTarget("localhost", 47181, UseTls: false) { Events = otherHost });
        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 1, UseTls: false) { Events = otherPort });

        Diagnostics.Assert("otherHost.Info", string.Join(" | ", new[] { "Host localhost:47181 was resolved.", "IPv6: (none)", "IPv4: 127.0.0.1", TryingLoopback }), string.Join(" | ", otherHost.Info));
        CollectionAssert.AreEqual(new[] { "Host localhost:47181 was resolved.", "IPv6: (none)", "IPv4: 127.0.0.1", TryingLoopback }, otherHost.Info);
        CollectionAssert.AreEqual(new[] { "  Trying 127.0.0.1:1..." }, otherPort.Info);
        CollectionAssert.AreEqual(new[] { "127.0.0.1", "localhost", "127.0.0.1" }, resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ConnectAsync_AfterADialThatReachedNoAddress_StillReportsTheHostFoundInDnsCache()
    {
        // curl -s -v http://127.0.0.1:1/a http://127.0.0.1:1/b -> both refused, and
        // * Hostname 127.0.0.1 was found in DNS cache before the second Trying.
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider());
        var second = new RecordingTransferEvents();

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 1, UseTls: false));
        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 1, UseTls: false) { Events = second });

        Diagnostics.Assert("second.Info[0]", "Hostname 127.0.0.1 was found in DNS cache", second.Info[0]);
        Assert.AreEqual("Hostname 127.0.0.1 was found in DNS cache", second.Info[0]);
        Assert.AreEqual("  Trying 127.0.0.1:1...", second.Info[1]);
    }

    [TestMethod]
    public async Task ConnectAsync_AfterAHostThatDidNotResolve_ResolvesItAgainWithoutTheFoundInDnsCacheLine()
    {
        // curl -s -v http://nonexistent.invalid:47181/a http://nonexistent.invalid:47181/b ->
        // Could not resolve host twice and Could not resolve: H:P for each, and no cache line
        // (BL-1157).
        var resolver = new FakeDnsResolver();
        var connector = CreateConnector(resolver, new FakeTcpDialer(), new FakeTlsProvider());
        var second = new RecordingTransferEvents();

        await ConnectLoggedAsync(connector, new ConnectTarget("nonexistent.invalid", 47181, UseTls: false));
        var result = await ConnectLoggedAsync(connector, new ConnectTarget("nonexistent.invalid", 47181, UseTls: false) { Events = second });

        Diagnostics.Assert("result.ExitCode", CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "Could not resolve host: nonexistent.invalid", "Could not resolve host: nonexistent.invalid", "Could not resolve: nonexistent.invalid:47181" },
            second.Info.ToArray());
        CollectionAssert.AreEqual(new[] { "nonexistent.invalid", "nonexistent.invalid" }, resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAResolveEntry_ReportsItAddedAndFoundInDnsCacheOnEveryTransferIncludingTheFirst()
    {
        // curl -s -v --resolve foo.example:47181:127.0.0.1 http://foo.example:47181/a http://foo.example:47181/b
        // -> * Added foo.example:47181:127.0.0.1 to DNS cache, then * Hostname foo.example was found in
        // DNS cache and the resolved lines before each transfer's Trying; the second transfer first
        // reports * RESOLVE foo.example:47181 - old addresses discarded (measured 2026-09-27, BL-482).
        var connector = CreateConnectorWithResolveEntries("foo.example:47181:127.0.0.1");
        var first = new RecordingTransferEvents();
        var second = new RecordingTransferEvents();

        await ConnectLoggedAsync(connector, new ConnectTarget("foo.example", 47181, UseTls: false) { Events = first });
        connector.LoadResolveEntries(second);
        await ConnectLoggedAsync(connector, new ConnectTarget("foo.example", 47181, UseTls: false) { Events = second });

        string[] resolved =
        [
            "Added foo.example:47181:127.0.0.1 to DNS cache",
            "Hostname foo.example was found in DNS cache",
            "Host foo.example:47181 was resolved.",
            "IPv6: (none)",
            "IPv4: 127.0.0.1",
            TryingLoopback,
        ];
        Diagnostics.Assert("first.Info", string.Join(" | ", resolved), string.Join(" | ", first.Info));
        CollectionAssert.AreEqual(resolved, first.Info);
        CollectionAssert.AreEqual(new[] { "RESOLVE foo.example:47181 - old addresses discarded" }.Concat(resolved).ToArray(), second.Info);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenNoTransferLoadedTheResolveEntries_LoadsThemOnTheFirstConnectOnly()
    {
        // curl -s -v -L --resolve foo.example:47181:127.0.0.1 http://foo.example:47181/a, redirected
        // to /b on a fresh connection -> no Added line before the second Hostname line (measured):
        // only a transfer's start, through LoadResolveEntries, loads them again.
        var connector = CreateConnectorWithResolveEntries("foo.example:47181:127.0.0.1");
        var events = new RecordingTransferEvents();
        var target = new ConnectTarget("foo.example", 47181, UseTls: false) { Events = events };

        await ConnectLoggedAsync(connector, target);
        await ConnectLoggedAsync(connector, target with { Events = new RecordingTransferEvents() });
        await ConnectLoggedAsync(connector, target);

        Diagnostics.Assert("events.Info.Count(line => line.StartsWith('Added ', StringComparison.Ordinal))", 1, events.Info.Count(line => line.StartsWith("Added ", StringComparison.Ordinal)));
        Assert.AreEqual(1, events.Info.Count(line => line.StartsWith("Added ", StringComparison.Ordinal)));
        Assert.AreEqual(2, events.Info.Count(line => line == "Hostname foo.example was found in DNS cache"));
    }

    [TestMethod]
    public async Task LoadResolveEntries_BeforeTheFirstConnect_ReportsTheEntriesAddedThereAndNotAgainInConnectAsync()
    {
        var connector = CreateConnectorWithResolveEntries("foo.example:47181:127.0.0.1");
        var transferStart = new RecordingTransferEvents();
        var connect = new RecordingTransferEvents();

        connector.LoadResolveEntries(transferStart);
        await ConnectLoggedAsync(connector, new ConnectTarget("foo.example", 47181, UseTls: false) { Events = connect });

        CollectionAssert.AreEqual(new[] { "Added foo.example:47181:127.0.0.1 to DNS cache" }, transferStart.Info);
        Diagnostics.Assert("connect.Info[0]", "Hostname foo.example was found in DNS cache", connect.Info[0]);
        Assert.AreEqual("Hostname foo.example was found in DNS cache", connect.Info[0]);
    }

    [TestMethod]
    public void LoadResolveEntries_WithoutEvents_Throws()
    {
        var connector = CreateConnectorWithResolveEntries();

        Diagnostics.Arrange("events", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => connector.LoadResolveEntries(null!));

        Diagnostics.Act("parameter name", exception.ParamName);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task ConnectAsync_WithSeveralAddressesInAResolveEntry_NamesThemVerbatimAndListsEachFamilyInOrder()
    {
        // curl -s -v --resolve foo.example:47181:127.0.0.1,127.0.0.2,[::1] http://foo.example:47181/a ->
        // * Added foo.example:47181:127.0.0.1,127.0.0.2,[::1] to DNS cache ... * IPv6: ::1 / * IPv4: 127.0.0.1, 127.0.0.2
        var connector = CreateConnectorWithResolveEntries("foo.example:47181:127.0.0.1,127.0.0.2,[::1]");
        var events = new RecordingTransferEvents();

        await ConnectLoggedAsync(connector, new ConnectTarget("foo.example", 47181, UseTls: false) { Events = events });

        Diagnostics.Assert("events.Info.Take(5).ToArray()", string.Join(" | ", new[] { "Added foo.example:47181:127.0.0.1,127.0.0.2,[::1] to DNS cache", "Hostname foo.example was found in DNS cache", "Host foo.example:47181 was resolved.", "IPv6: ::1", "IPv4: 127.0.0.1, 127.0.0.2", }), string.Join(" | ", events.Info.Take(5).ToArray()));
        CollectionAssert.AreEqual(
            new[]
            {
                "Added foo.example:47181:127.0.0.1,127.0.0.2,[::1] to DNS cache",
                "Hostname foo.example was found in DNS cache",
                "Host foo.example:47181 was resolved.",
                "IPv6: ::1",
                "IPv4: 127.0.0.1, 127.0.0.2",
            },
            events.Info.Take(5).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_WithAWildcardResolveEntry_ReportsItNonPermanentAndTheWildcardAsTheResolvedHost()
    {
        // curl -s -v --resolve +*:47181:127.0.0.1 http://127.0.0.1:47181/b ->
        // * Added *:47181:127.0.0.1 to DNS cache (non-permanent) / * RESOLVE *:47181 using wildcard /
        // * Hostname 127.0.0.1 was found in DNS cache / * Host *:47181 was resolved. (measured)
        var connector = CreateConnectorWithResolveEntries("+*:47181:127.0.0.1");
        var events = new RecordingTransferEvents();

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 47181, UseTls: false) { Events = events });

        Diagnostics.Assert("events.Info", string.Join(" | ", new[] { "Added *:47181:127.0.0.1 to DNS cache (non-permanent)", "RESOLVE *:47181 using wildcard", "Hostname 127.0.0.1 was found in DNS cache", "Host *:47181 was resolved.", "IPv6: (none)", "IPv4: 127.0.0.1", TryingLoopback, }), string.Join(" | ", events.Info));
        CollectionAssert.AreEqual(
            new[]
            {
                "Added *:47181:127.0.0.1 to DNS cache (non-permanent)",
                "RESOLVE *:47181 using wildcard",
                "Hostname 127.0.0.1 was found in DNS cache",
                "Host *:47181 was resolved.",
                "IPv6: (none)",
                "IPv4: 127.0.0.1",
                TryingLoopback,
            },
            events.Info);
    }

    [TestMethod]
    public async Task ConnectAsync_WithARemovalEntry_DropsTheKeySilentlySoTheNextTransferDiscardsNothingForIt()
    {
        // curl -s -v --resolve FOO.example:47181:127.0.0.1 --resolve -foo.example:47181
        // --resolve bar.example:47181:127.0.0.1 http://127.0.0.1:47181/a http://Bar.Example:47181/b ->
        // the second transfer discards bar.example only, and names the entry's host as written (measured).
        var connector = CreateConnectorWithResolveEntries("FOO.example:47181:127.0.0.1", "-foo.example:47181", "bar.example:47181:127.0.0.1");
        var first = new RecordingTransferEvents();
        var second = new RecordingTransferEvents();

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 47181, UseTls: false) { Events = first });
        connector.LoadResolveEntries(second);
        await ConnectLoggedAsync(connector, new ConnectTarget("Bar.Example", 47181, UseTls: false) { Events = second });

        Diagnostics.Assert("first.Info", string.Join(" | ", new[] { "Added FOO.example:47181:127.0.0.1 to DNS cache", "Added bar.example:47181:127.0.0.1 to DNS cache", TryingLoopback }), string.Join(" | ", first.Info));
        CollectionAssert.AreEqual(
            new[] { "Added FOO.example:47181:127.0.0.1 to DNS cache", "Added bar.example:47181:127.0.0.1 to DNS cache", TryingLoopback },
            first.Info);
        CollectionAssert.AreEqual(
            new[]
            {
                "Added FOO.example:47181:127.0.0.1 to DNS cache",
                "RESOLVE bar.example:47181 - old addresses discarded",
                "Added bar.example:47181:127.0.0.1 to DNS cache",
                "Hostname Bar.Example was found in DNS cache",
                "Host bar.example:47181 was resolved.",
                "IPv6: (none)",
                "IPv4: 127.0.0.1",
                TryingLoopback,
            },
            second.Info);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAResolveEntryThatDoesNotParse_ReportsTheEntriesBeforeItAddedAndFailsWithExit49()
    {
        // curl -s -v --resolve Foo.Example:47181:127.0.0.1 --resolve bad http://127.0.0.1:47181/a ->
        // * Added Foo.Example:47181:127.0.0.1 to DNS cache, then exit 49 (measured).
        var connector = CreateConnectorWithResolveEntries("Foo.Example:47181:127.0.0.1", "bad");
        var events = new RecordingTransferEvents();

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 47181, UseTls: false) { Events = events });

        Diagnostics.Assert("result.ExitCode", CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "Added Foo.Example:47181:127.0.0.1 to DNS cache" }, events.Info);
    }

    private static TcpConnector CreateConnectorWithResolveEntries(params string[] entries) =>
        new(
            new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(entries));

    [TestMethod]
    public async Task ConnectAsync_WithAConnectToMapping_KeysTheDnsCacheOnTheMappedHostAndPort()
    {
        // curl -s -v --connect-to 127.0.0.1:47182:127.0.0.1:47181 http://127.0.0.1:47181/a http://127.0.0.1:47182/b
        // -> * Hostname 127.0.0.1 was found in DNS cache before the second Trying 127.0.0.1:47181;
        // curl --connect-to foo.example:47181:localhost:47181 ... names localhost, the mapped host.
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider(),
            connectToMappings: new ConnectToMappings(["foo.example:47181:127.0.0.1:47181"]));
        var first = new RecordingTransferEvents();
        var second = new RecordingTransferEvents();

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 47181, UseTls: false) { Events = first });
        await ConnectLoggedAsync(connector, new ConnectTarget("foo.example", 47181, UseTls: false) { Events = second });

        Diagnostics.Assert("first.Info", string.Join(" | ", new[] { TryingLoopback }), string.Join(" | ", first.Info));
        CollectionAssert.AreEqual(new[] { TryingLoopback }, first.Info);
        CollectionAssert.AreEqual(new[] { "Hostname 127.0.0.1 was found in DNS cache", TryingLoopback }, second.Info);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAProxyAlreadyResolved_ReportsTheProxyHostFoundInDnsCache()
    {
        // curl -s -v -x http://127.0.0.1:47181 http://foo.example/a http://foo.example/b ->
        // * Hostname 127.0.0.1 was found in DNS cache before the second Trying: the proxy's host.
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider());
        var target = new ConnectTarget("foo.example", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null) };
        var second = new RecordingTransferEvents();

        await ConnectLoggedAsync(connector, target);
        await ConnectLoggedAsync(connector, target with { Events = second });

        Diagnostics.Assert("second.Info[0]", "Hostname proxy.example was found in DNS cache", second.Info[0]);
        Assert.AreEqual("Hostname proxy.example was found in DNS cache", second.Info[0]);
    }
}
