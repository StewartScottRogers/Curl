using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <c>--interface</c> and <c>--local-port</c> through <see cref="TcpConnector" />'s
/// <c>localBinding</c> (BL-600): the local end each dial asks <see cref="ITcpDialer.DialFromAsync(IPEndPoint, IPEndPoint, int, ITransferEvents, CancellationToken)" />
/// for, and exit 45, 43 and 7 as curl 8.21.0 ends a connect whose local end cannot be bound
/// (measured on Windows 2026-09-29, BL-600 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private static readonly IPEndPoint BindTarget = new(IPAddress.Loopback, 47599);

    [TestMethod]
    public async Task ConnectAsync_WithInterfaceAddressAndPortRange_BindsThatAddressFromTheFirstPort()
    {
        // curl --interface 127.0.0.1 --local-port 40000-40010 -w '%{local_ip}' -> 127.0.0.1, exit 0.
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var binding = new LocalBinding("127.0.0.1", "127.0.0.1", null, 40000, 11);
        var connector = LocalBindingConnector(dialer, binding);

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47599, UseTls: false), CancellationToken.None);

        Assert.IsNotNull(result.Connection);
        Assert.AreSame(binding, connector.LocalBinding);
        Assert.AreEqual((BindTarget, new IPEndPoint(IPAddress.Loopback, 40000), 11), dialer.BoundDials.Single());
        Assert.IsEmpty(dialer.DialedEndPoints.Except([BindTarget]));
    }

    [TestMethod]
    [DataRow("127.0.0.1", "0.0.0.0")]
    [DataRow("::1", "::")]
    public async Task ConnectAsync_WithLocalPortsOnly_BindsTheUnspecifiedAddressOfTheFamilyDialled(string host, string localAddress)
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = LocalBindingConnector(dialer, new LocalBinding(null, null, null, 40000, 3));

        var result = await connector.ConnectAsync(new ConnectTarget(host, 47599, UseTls: false), CancellationToken.None);

        Assert.IsNotNull(result.Connection);
        Assert.AreEqual(new IPEndPoint(IPAddress.Parse(localAddress), 40000), dialer.BoundDials.Single().LocalEndPoint);
        Assert.AreEqual(3, dialer.BoundDials.Single().LocalPortCount);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenNoPortOfTheRangeBinds_FailsWithInterfaceFailed()
    {
        // curl --local-port 40000-40002 (all in use) -> exit 45,
        // curl: (45) Failed to connect to 127.0.0.1:47599 after 0 ms: Failed binding local connection end;
        // -v: connect to 127.0.0.1 port 47599 from  port 0 failed: No error.
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer
        {
            DialOutcome = _ => new FakeConnection(),
            BindOutcome = (_, _) => throw new LocalBindException(LocalBindFailure.InterfaceFailed),
        };
        var connector = LocalBindingConnector(dialer, new LocalBinding(null, null, null, 40000, 3));

        var result = await connector.ConnectAsync(
            new ConnectTarget("127.0.0.1", 47599, UseTls: false) { Events = events },
            CancellationToken.None);

        const string message = "Failed to connect to 127.0.0.1:47599 after 0 ms: Failed binding local connection end";
        Assert.AreEqual(CurlExitCode.InterfaceFailed, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying 127.0.0.1:47599...",
                "connect to 127.0.0.1 port 47599 from  port 0 failed: " + LocalBindException.ReasonText(OperatingSystem.IsWindows()),
                message,
            },
            events.Info);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAnInterfaceNameOnlyTheLookupDoesNotFind_FailsWithInterfaceFailedWithoutDialling()
    {
        // curl --interface if!bogus0, and on Windows every if! name -> exit 45.
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = LocalBindingConnector(dialer, new LocalBinding("bogus0", null, null, 0, 1), Interfaces(("lo", [IPAddress.Loopback])));

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47599, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.InterfaceFailed, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1:47599 after 0 ms: Failed binding local connection end", result.ErrorMessage);
        Assert.IsEmpty(dialer.BoundDials);
    }

    [TestMethod]
    [DataRow("127.0.0.1", "127.0.0.1")]
    [DataRow("::1", "::1")]
    public async Task ConnectAsync_WithAnInterfaceNameTheLookupFinds_BindsItsAddressOfTheFamilyDialled(string host, string localAddress)
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = LocalBindingConnector(
            dialer,
            new LocalBinding("lo", "lo", null, 0, 1),
            Interfaces(("eth0", [IPAddress.Parse("192.0.2.1")]), ("lo", [IPAddress.IPv6Loopback, IPAddress.Loopback])));

        var result = await connector.ConnectAsync(new ConnectTarget(host, 47599, UseTls: false), CancellationToken.None);

        Assert.IsNotNull(result.Connection);
        Assert.AreEqual(new IPEndPoint(IPAddress.Parse(localAddress), 0), dialer.BoundDials.Single().LocalEndPoint);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAnInterfaceWithNoAddressOfTheFamilyDialled_FailsWithCouldntConnect()
    {
        // libcurl's IF2IP_AF_NOT_SUPPORTED: the address is given up as unsupported, and the connect ends in exit 7.
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = LocalBindingConnector(dialer, new LocalBinding("lo", null, null, 0, 1), Interfaces(("lo", [IPAddress.IPv6Loopback])));

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47599, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1:47599 after 0 ms: Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAPlainNameNoInterfaceHas_ResolvesItAsAHost()
    {
        // curl --interface <name> tries the name as an interface, then as a host name.
        var resolver = new HostMapDnsResolver(new() { ["127.0.0.1"] = [IPAddress.Loopback], ["me.test"] = [IPAddress.Parse("127.0.0.2")] });
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(
            resolver,
            dialer,
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            localBinding: new LocalBinding("me.test", "me.test", null, 0, 1),
            networkInterfaceLookup: Interfaces());

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47599, UseTls: false), CancellationToken.None);

        Assert.IsNotNull(result.Connection);
        Assert.AreEqual(new IPEndPoint(IPAddress.Parse("127.0.0.2"), 0), dialer.BoundDials.Single().LocalEndPoint);
    }

    [TestMethod]
    [DataRow("nosuch.invalid")]
    [DataRow(" ")]
    public async Task ConnectAsync_WithAHostNameThatDoesNotResolve_FailsWithInterfaceFailed(string hostName)
    {
        // curl --interface host!nosuch.invalid, and --interface ' ' -> exit 45.
        var resolver = new HostMapDnsResolver(new() { ["127.0.0.1"] = [IPAddress.Loopback] });
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(
            resolver,
            dialer,
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            localBinding: new LocalBinding(null, hostName, null, 0, 1),
            networkInterfaceLookup: Interfaces());

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47599, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.InterfaceFailed, result.ExitCode);
    }

    [TestMethod]
    [DataRow("127.0.0.1", CurlExitCode.CouldntConnect)]
    [DataRow("::1", CurlExitCode.Ok)]
    public async Task ConnectAsync_WithHostLocalhost_BindsItsFirstAddressWhateverTheFamilyDialled(string host, CurlExitCode exitCode)
    {
        // curl --interface host!localhost http://127.0.0.1:47599/ -> exit 7, with -4 too: localhost is ::1 first.
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = LocalBindingConnector(dialer, new LocalBinding(null, "localhost", null, 0, 1));

        var result = await connector.ConnectAsync(new ConnectTarget(host, 47599, UseTls: false), CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(exitCode == CurlExitCode.Ok ? 1 : 0, dialer.BoundDials.Count);
    }

    [TestMethod]
    [DataRow(254, CurlExitCode.Ok)]
    [DataRow(255, CurlExitCode.BadFunctionArgument)]
    public async Task ConnectAsync_WithAnInterfaceAndHost_BindsTheHostUnlessTheInterfacePartIsTooLong(int deviceLength, CurlExitCode exitCode)
    {
        // curl --interface ifhost!<255 a's>!127.0.0.1 -> exit 43,
        // curl: (43) Failed to connect to 127.0.0.1:47599 after 0 ms: A libcurl function was given a bad argument.
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = LocalBindingConnector(dialer, new LocalBinding(null, "127.0.0.1", new string('a', deviceLength), 0, 1));

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47599, UseTls: false), CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        if (exitCode == CurlExitCode.Ok)
        {
            Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 0), dialer.BoundDials.Single().LocalEndPoint);
        }
        else
        {
            Assert.AreEqual("Failed to connect to 127.0.0.1:47599 after 0 ms: A libcurl function was given a bad argument", result.ErrorMessage);
        }
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheFirstAddressCannotBindAndTheNextCan_ConnectsTheNext()
    {
        // curl --interface 127.0.0.1 http://localhost:47599/: ::1 fails (family), 127.0.0.1 connects.
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(
            new FakeDnsResolver(IPAddress.IPv6Loopback, IPAddress.Loopback),
            dialer,
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            localBinding: new LocalBinding("127.0.0.1", "127.0.0.1", null, 0, 1),
            networkInterfaceLookup: Interfaces());

        var result = await connector.ConnectAsync(new ConnectTarget("localhost", 47599, UseTls: false), CancellationToken.None);

        Assert.IsNotNull(result.Connection);
        Assert.AreEqual(BindTarget, dialer.BoundDials.Single().EndPoint);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenEveryAddressFailsToBind_FailsAsTheLastOneDid()
    {
        // curl --interface host!nosuch.invalid http://localhost:47599/ -> both addresses fail to bind, exit 45.
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer
        {
            DialOutcome = _ => new FakeConnection(),
            BindOutcome = (_, _) => throw new LocalBindException(LocalBindFailure.InterfaceFailed),
        };
        var connector = new TcpConnector(
            new FakeDnsResolver(IPAddress.IPv6Loopback, IPAddress.Loopback),
            dialer,
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            localBinding: new LocalBinding(null, null, null, 0, 1));

        var result = await connector.ConnectAsync(new ConnectTarget("localhost", 47599, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.InterfaceFailed, result.ExitCode);
        Assert.AreEqual("Failed to connect to localhost:47599 after 0 ms: Failed binding local connection end", result.ErrorMessage);
        Assert.HasCount(2, dialer.BoundDials);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenABindFailureIsFollowedByARefusal_FailsAsRefused()
    {
        var dialer = new FakeTcpDialer
        {
            BindOutcome = (local, _) =>
            {
                if (local.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    throw new LocalBindException(LocalBindFailure.InterfaceFailed);
                }
            },
        };
        var connector = new TcpConnector(
            new FakeDnsResolver(IPAddress.IPv6Loopback, IPAddress.Loopback),
            dialer,
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            localBinding: new LocalBinding(null, null, null, 0, 1));

        var result = await connector.ConnectAsync(new ConnectTarget("localhost", 47599, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual("Failed to connect to localhost:47599 after 0 ms: Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAProxyWhoseDialCannotBind_FailsWithInterfaceFailedOverProxy()
    {
        var dialer = new FakeTcpDialer
        {
            DialOutcome = _ => new FakeConnection(),
            BindOutcome = (_, _) => throw new LocalBindException(LocalBindFailure.InterfaceFailed),
        };
        var connector = LocalBindingConnector(dialer, new LocalBinding(null, null, null, 40000, 1));

        var result = await connector.ConnectAsync(
            new ConnectTarget("example.com", 443, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 3128, null) },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.InterfaceFailed, result.ExitCode);
        Assert.AreEqual("Failed to connect to example.com:443 over proxy 127.0.0.1 after 0 ms: Failed binding local connection end", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ConnectAsync_OnWindowsWithAnInterfaceNameOnly_FailsWithInterfaceFailed()
    {
        // The Schannel build has no getifaddrs: curl --interface "if!Loopback Pseudo-Interface 1" -> exit 45.
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(
            new HostMapDnsResolver([]),
            dialer,
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            localBinding: new LocalBinding(LoopbackInterfaceName(), null, null, 0, 1));

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47599, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.InterfaceFailed, result.ExitCode);
        Assert.IsEmpty(dialer.BoundDials);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ConnectAsync_OffWindowsWithTheLoopbackInterfaceName_BindsItsAddress()
    {
        // The OpenSSL build finds the interface with getifaddrs: curl --interface if!lo binds 127.0.0.1.
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(
            new HostMapDnsResolver([]),
            dialer,
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            localBinding: new LocalBinding(LoopbackInterfaceName(), null, null, 0, 1));

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47599, UseTls: false), CancellationToken.None);

        Assert.IsNotNull(result.Connection);
        Assert.AreEqual(IPAddress.Loopback, dialer.BoundDials.Single().LocalEndPoint.Address);
    }

    private static TcpConnector LocalBindingConnector(FakeTcpDialer dialer, LocalBinding binding, INetworkInterfaceLookup? interfaces = null) =>
        new(
            new HostMapDnsResolver([]),
            dialer,
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            localBinding: binding,
            networkInterfaceLookup: interfaces ?? Interfaces());

    private static SystemNetworkInterfaceLookup Interfaces(params (string Name, IPAddress[] Addresses)[] interfaces) =>
        new(onWindows: false, () => interfaces);

    private static string LoopbackInterfaceName() =>
        NetworkInterface.GetAllNetworkInterfaces().First(candidate => candidate.NetworkInterfaceType == NetworkInterfaceType.Loopback).Name;

    /// <summary>A resolver that answers each host from a map, an IP address as written, and nothing for any other.</summary>
    private sealed class HostMapDnsResolver(Dictionary<string, IPAddress[]> answers) : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>(
                answers.GetValueOrDefault(host) ?? (IPAddress.TryParse(host, out var address) ? [address] : []));
    }
}
