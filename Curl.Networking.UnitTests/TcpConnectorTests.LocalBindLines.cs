using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The <c>-v</c> lines curl writes while it binds the local end for <c>--interface</c> (BL-1027), between
/// each <c>Trying</c> and the connect outcome, measured with <c>Record-CurlExchange.ps1</c> against curl
/// 8.21.0 on Windows and curl 8.18.0 on Linux on 2026-10-01 (BL-1027 Notes). The port lines are
/// <see cref="TcpDialer.BindLocalEnd" />'s, pinned in <c>TcpDialerTests</c>.
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_WithAnInterfaceAddressOfTheFamilyDialled_ReportsTheNameResolvedThenConnects()
    {
        // curl --interface 127.0.0.1 --local-port 40010-40012 http://127.0.0.1:47601/, both platforms:
        // "Trying 127.0.0.1:47601...", "Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2", "Local port: 40010".
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = LocalBindingConnector(dialer, new LocalBinding("127.0.0.1", "127.0.0.1", null, 40010, 3));

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47599, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.IsNotNull(result.Connection);
        CollectionAssert.AreEqual(
            new[] { "  Trying 127.0.0.1:47599...", "Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2" },
            events.Info.Take(2).ToArray());
        Assert.AreSame(events, dialer.BoundDialEvents.Single());
    }

    [TestMethod]
    public async Task ConnectAsync_WithPortsOnly_ReportsNoNameAndHandsTheDialerTheEvents()
    {
        // curl --local-port 40020 http://127.0.0.1:47601/: "Trying 127.0.0.1:47601...", then "Local port: 40020".
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = LocalBindingConnector(dialer, new LocalBinding(null, null, null, 40020, 1));

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47599, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.AreEqual("  Trying 127.0.0.1:47599...", events.Info[0]);
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("Name ", StringComparison.Ordinal)));
        Assert.AreSame(events, dialer.BoundDialEvents.Single());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ConnectAsync_WithAnInterfaceAddressToLocalhostOnWindows_ReportsTheFamilyMismatchThenTheBind()
    {
        // curl 8.21.0 Windows, --interface 127.0.0.1 http://localhost:47601/.
        var lines = await LocalhostWithIPv4InterfaceLinesAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying [::1]:47599...",
                "Name '127.0.0.1' family 23 resolved to '127.0.0.1' family 2",
                "connect to ::1 port 47599 from  port 0 failed: No error",
                "  Trying 127.0.0.1:47599...",
                "Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2",
            },
            lines);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task ConnectAsync_WithAnInterfaceAddressToLocalhostOnLinux_ReportsTheFamilyMismatchThenTheBind()
    {
        // curl 8.18.0 Linux, --interface 127.0.0.1 http://localhost:47601/: AF_INET6 is 10 there.
        var lines = await LocalhostWithIPv4InterfaceLinesAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying [::1]:47599...",
                "Name '127.0.0.1' family 10 resolved to '127.0.0.1' family 2",
                "connect to ::1 port 47599 from  port 0 failed: Success",
                "  Trying 127.0.0.1:47599...",
                "Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2",
            },
            lines);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("::1", "Name '::1' family 2 resolved to '::1' family 23", DisplayName = "::1")]
    [DataRow("localhost", "Name 'localhost' family 2 resolved to '::1' family 23", DisplayName = "host!localhost")]
    public async Task ConnectAsync_WithAnIPv6HostToAnIPv4AddressOnWindows_ReportsTheNameResolvedAndNoBind(string hostName, string nameLine)
    {
        // curl 8.21.0 Windows, --interface ::1 and host!localhost to http://127.0.0.1:47601/ -> exit 7.
        var lines = await IPv6HostToIPv4AddressLinesAsync(hostName);

        CollectionAssert.AreEqual(
            new[] { "  Trying 127.0.0.1:47599...", nameLine, "connect to 127.0.0.1 port 47599 from  port 0 failed: No error" },
            lines);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    [DataRow("::1", "Name '::1' family 2 resolved to '::1' family 10", DisplayName = "::1")]
    [DataRow("localhost", "Name 'localhost' family 2 resolved to '::1' family 10", DisplayName = "host!localhost")]
    public async Task ConnectAsync_WithAnIPv6HostToAnIPv4AddressOnLinux_ReportsTheNameResolvedAndNoBind(string hostName, string nameLine)
    {
        // curl 8.18.0 Linux, --interface ::1 and host!localhost to http://127.0.0.1:47601/ -> exit 7.
        var lines = await IPv6HostToIPv4AddressLinesAsync(hostName);

        Assert.AreEqual(nameLine, lines[1]);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ConnectAsync_WithAHostThatDoesNotResolveOnWindows_ReportsTheResolveAndBindFailures()
    {
        // curl 8.21.0 Windows, --interface bogus0 http://127.0.0.1:47601/ -> exit 45.
        var lines = await UnresolvableHostLinesAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying 127.0.0.1:47599...",
                "Could not resolve host: bogus0",
                "Could not bind to 'bogus0' with errno 0: No error",
                "connect to 127.0.0.1 port 47599 from  port 0 failed: No error",
            },
            lines);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ConnectAsync_WithAHostThatDoesNotResolveOffWindows_ReportsErrno22()
    {
        // curl 8.18.0 Linux, --interface bogus0 and host!nosuch.invalid -> "with errno 22: Invalid argument".
        var lines = await UnresolvableHostLinesAsync();

        CollectionAssert.AreEqual(
            new[] { "Could not resolve host: bogus0", "Could not bind to 'bogus0' with errno 22: Invalid argument" },
            lines.Skip(1).Take(2).ToArray());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ConnectAsync_WithAnInterfaceOnlyNameNoInterfaceHasOnWindows_ReportsTheInterfaceBindFailure()
    {
        // curl 8.21.0 Windows, --interface if!Ethernet http://127.0.0.1:47601/ -> exit 45.
        var lines = await MissingInterfaceLinesAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying 127.0.0.1:47599...",
                "Could not bind to interface 'Ethernet' with errno 0: No error",
                "connect to 127.0.0.1 port 47599 from  port 0 failed: No error",
            },
            lines);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ConnectAsync_WithAnInterfaceOnlyNameNoInterfaceHasOffWindows_ReportsErrno19()
    {
        // curl 8.18.0 Linux, --interface if!bogus0 -> "Could not bind to interface 'bogus0' with errno 19: No such device".
        var lines = await MissingInterfaceLinesAsync();

        Assert.AreEqual("Could not bind to interface 'Ethernet' with errno 19: No such device", lines[1]);
    }

    private static async Task<string[]> LocalhostWithIPv4InterfaceLinesAsync()
    {
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(
            new FakeDnsResolver(IPAddress.IPv6Loopback, IPAddress.Loopback),
            dialer,
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            localBinding: new LocalBinding("127.0.0.1", "127.0.0.1", null, 0, 1),
            networkInterfaceLookup: Interfaces());

        await connector.ConnectAsync(new ConnectTarget("localhost", 47599, UseTls: false) { Events = events }, CancellationToken.None);

        return LinesFromTheFirstTrying(events).Take(5).ToArray();
    }

    private static async Task<string[]> IPv6HostToIPv4AddressLinesAsync(string hostName)
    {
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = LocalBindingConnector(dialer, new LocalBinding(null, hostName, null, 0, 1));

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47599, UseTls: false) { Events = events }, CancellationToken.None);

        return LinesFromTheFirstTrying(events).Take(3).ToArray();
    }

    private static async Task<string[]> UnresolvableHostLinesAsync()
    {
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = LocalBindingConnector(dialer, new LocalBinding("bogus0", "bogus0", null, 0, 1));

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47599, UseTls: false) { Events = events }, CancellationToken.None);

        return LinesFromTheFirstTrying(events).Take(4).ToArray();
    }

    private static async Task<string[]> MissingInterfaceLinesAsync()
    {
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = LocalBindingConnector(dialer, new LocalBinding("Ethernet", null, null, 0, 1));

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47599, UseTls: false) { Events = events }, CancellationToken.None);

        return LinesFromTheFirstTrying(events).Take(3).ToArray();
    }

    private static IEnumerable<string> LinesFromTheFirstTrying(RecordingTransferEvents events) =>
        events.Info.SkipWhile(line => !line.StartsWith("  Trying", StringComparison.Ordinal));
}
