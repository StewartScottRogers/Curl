using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins a <c>-P</c> value that names a network interface (BL-474, ADR-0110): off Windows curl
/// asks <c>Curl_if2ip</c> first and announces the interface's address of the control
/// connection's family and IPv6 scope, and only a name that is no interface goes to the
/// resolver; on Windows the lookup finds nothing, so every name goes to the resolver. The
/// <c>lo</c> and <c>eth0</c> cases were recorded on 2026-09-27 with
/// <c>Record-CurlExchange.ps1 -Ftp -ListenAddress</c> from the OpenSSL build of curl in WSL
/// Ubuntu, with <c>EPRT</c> refused so no data connection was needed.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerInterfaceNameTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://127.0.0.1:47471/a.txt";

    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string LogInSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n";

    private const string Retrieved = "200 EPRT command successful\r\n200 Type set\r\n213 5\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    private const string RetrieveSent = "TYPE I\r\nSIZE a.txt\r\nRETR a.txt\r\nQUIT\r\n";

    [TestMethod]
    [DataRow("lo", "127.0.0.1", 37667)]
    [DataRow("LO", "127.0.0.1", 58711)]
    [DataRow("eth0", "172.26.99.197", 38907)]
    public async Task ExecuteAsync_PortNamesAnInterface_ListensOnAndAnnouncesItsAddressWithoutResolving(string name, string expected, int port)
    {
        // curl -v -P lo ftp://172.26.96.1:47471/f.txt (Linux, OpenSSL): EPRT |1|127.0.0.1|37667|.
        // curl compares the name without regard to case, so -P LO is lo.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("-P value", name);
        diagnostics.Arrange("expected address", expected);
        diagnostics.Arrange("expected port", port);
        var lookup = new NamedNetworkInterfaceLookup(new Dictionary<string, IPAddress[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["lo"] = [IPAddress.Loopback, IPAddress.Parse("10.255.255.254"), IPAddress.IPv6Loopback],
            ["eth0"] = [IPAddress.Parse("172.26.99.197"), IPAddress.Parse("fe80::215:5dff:fe30:7a97")],
        });
        var resolver = new NamedDnsResolver(new Dictionary<string, IPAddress[]>());

        InterfaceRun run = await RunAsync(diagnostics, name, LoggedIn + Retrieved, lookup, resolver, "172.26.99.197", Pending(port, expected));

        diagnostics.DiffSent(LogInSent + $"EPRT |1|{expected}|{port}|\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(LogInSent + $"EPRT |1|{expected}|{port}|\r\n" + RetrieveSent, run.Sent);
        diagnostics.Assert("looked-up names", name, string.Join(",", lookup.Names));
        CollectionAssert.AreEqual(new[] { name }, lookup.Names);
        diagnostics.Assert("resolved hosts", string.Empty, string.Join(",", resolver.Hosts));
        Assert.IsEmpty(resolver.Hosts);
        diagnostics.Assert("listen target", new ListenTarget(IPAddress.Parse(expected), 0, 0), run.Listener.Targets.Single());
        Assert.AreEqual(new ListenTarget(IPAddress.Parse(expected), 0, 0), run.Listener.Targets.Single());
        diagnostics.Assert("result", TransferResult.Success(5), run.Result);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    [DataRow("::1", "fe80::1,::1", "::1")]
    [DataRow("fe80::5", "::1,fe80::1%3", "fe80::1")]
    [DataRow("fd00::5", "fe80::1,fd00::1", "fd00::1")]
    [DataRow("fec0::5", "fd00::1,fec0::1", "fec0::1")]
    [DataRow("2001:db8::5", "fe80::1,::1,2001:db8::1", "2001:db8::1")]
    [DataRow("127.0.0.1", "::1,127.0.0.1", "127.0.0.1")]
    [DataRow("::ffff:127.0.0.1", "::1,127.0.0.1", "127.0.0.1")]
    public async Task ExecuteAsync_PortNamesAnInterface_UsesItsFirstAddressOfTheControlFamilyAndScope(string control, string addresses, string expected)
    {
        // Curl_if2ip keeps an IPv6 address only when Curl_ipv6_scope gives it the control
        // connection's scope, and announces it as inet_ntop writes it, with no scope ID.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("control address", control);
        diagnostics.Arrange("interface addresses", addresses);
        diagnostics.Arrange("expected address", expected);
        var lookup = new NamedNetworkInterfaceLookup(new Dictionary<string, IPAddress[]>
        {
            ["if0"] = [.. addresses.Split(',').Select(IPAddress.Parse)],
        });
        IPAddress announced = IPAddress.Parse(expected);
        string family = announced.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? "2" : "1";

        InterfaceRun run = await RunAsync(diagnostics, "if0", LoggedIn + Retrieved, lookup, new NamedDnsResolver(new Dictionary<string, IPAddress[]>()), control, Pending(40001, expected));

        diagnostics.DiffSent(LogInSent + $"EPRT |{family}|{expected}|40001|\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(LogInSent + $"EPRT |{family}|{expected}|40001|\r\n" + RetrieveSent, run.Sent);
        diagnostics.Assert("listen target", new ListenTarget(announced, 0, 0), run.Listener.Targets.Single());
        Assert.AreEqual(new ListenTarget(announced, 0, 0), run.Listener.Targets.Single());
        diagnostics.Assert("result", TransferResult.Success(5), run.Result);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    [DataRow("127.0.0.1", "::1")]
    [DataRow("::1", "127.0.0.1,fe80::1")]
    [DataRow(null, "127.0.0.1")]
    public async Task ExecuteAsync_PortInterfaceHasNoAddressOfTheControlFamilyAndScope_QuitsWithExit30(string? control, string addresses)
    {
        // Curl_if2ip answers IF2IP_AF_NOT_SUPPORTED, and ftp_state_use_port ends with
        // CURLE_FTP_PORT_FAILED without resolving the name.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("control address", control);
        diagnostics.Arrange("interface addresses", addresses);
        var lookup = new NamedNetworkInterfaceLookup(new Dictionary<string, IPAddress[]>
        {
            ["if0"] = [.. addresses.Split(',').Select(IPAddress.Parse)],
        });
        var resolver = new NamedDnsResolver(new Dictionary<string, IPAddress[]>());

        InterfaceRun run = await RunAsync(diagnostics, "if0", LoggedIn + "221 Bye\r\n", lookup, resolver, control);

        diagnostics.DiffSent(LogInSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(LogInSent + "QUIT\r\n", run.Sent);
        diagnostics.Assert("resolved hosts", string.Empty, string.Join(",", resolver.Hosts));
        Assert.IsEmpty(resolver.Hosts);
        diagnostics.Assert("listen targets", 0, run.Listener.Targets.Count);
        Assert.AreEqual(0, run.Listener.Targets.Count);
        diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.FtpPortFailed, "Failed to do PORT"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpPortFailed, "Failed to do PORT"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PortNameIsNoInterfaceAsEveryNameOnWindows_GoesToTheResolver()
    {
        // The Schannel build has no getifaddrs, so its lookup finds no interface and
        // -P "Loopback Pseudo-Interface 1" was resolved as a host name (ADR-0108); off Windows
        // a name that is no interface is resolved the same way.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("-P value", "Loopback Pseudo-Interface 1");
        var lookup = new NamedNetworkInterfaceLookup(new Dictionary<string, IPAddress[]>());
        var resolver = new NamedDnsResolver(new Dictionary<string, IPAddress[]>
        {
            ["Loopback Pseudo-Interface 1"] = [IPAddress.Loopback],
        });

        InterfaceRun run = await RunAsync(diagnostics, "Loopback Pseudo-Interface 1", LoggedIn + Retrieved, lookup, resolver, "127.0.0.1", Pending(40002, "127.0.0.1"));

        diagnostics.DiffSent(LogInSent + "EPRT |1|127.0.0.1|40002|\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(LogInSent + "EPRT |1|127.0.0.1|40002|\r\n" + RetrieveSent, run.Sent);
        diagnostics.Assert("looked-up names", "Loopback Pseudo-Interface 1", string.Join(",", lookup.Names));
        CollectionAssert.AreEqual(new[] { "Loopback Pseudo-Interface 1" }, lookup.Names);
        diagnostics.Assert("resolved hosts", "Loopback Pseudo-Interface 1", string.Join(",", resolver.Hosts));
        CollectionAssert.AreEqual(new[] { "Loopback Pseudo-Interface 1" }, resolver.Hosts);
        diagnostics.Assert("result", TransferResult.Success(5), run.Result);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PortDash_IsNotLookedUp()
    {
        // -P - is the control connection's own address; curl asks Curl_if2ip nothing.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("-P value", "-");
        var lookup = new NamedNetworkInterfaceLookup(new Dictionary<string, IPAddress[]>());

        InterfaceRun run = await RunAsync(diagnostics, "-", LoggedIn + Retrieved, lookup, new NamedDnsResolver(new Dictionary<string, IPAddress[]>()), "127.0.0.1", Pending(40003, "127.0.0.1"));

        diagnostics.DiffSent(LogInSent + "EPRT |1|127.0.0.1|40003|\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(LogInSent + "EPRT |1|127.0.0.1|40003|\r\n" + RetrieveSent, run.Sent);
        diagnostics.Assert("looked-up names", string.Empty, string.Join(",", lookup.Names));
        Assert.IsEmpty(lookup.Names);
    }

    [TestMethod]
    public void Constructor_NullInterfaceLookup_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("interface lookup", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new FtpProtocolHandler(
                new QueuedConnector(),
                new QueuedListener(),
                new QueuedTlsProvider(),
                new NamedDnsResolver(new Dictionary<string, IPAddress[]>()),
                null!));

        diagnostics.Act("throws", exception.GetType().Name);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    private static ScriptedPendingConnection Pending(int port, string address) =>
        new(new IPEndPoint(IPAddress.Parse(address), port), ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes("hello"))));

    private static async Task<InterfaceRun> RunAsync(
        TestDiagnostics diagnostics,
        string ftpPort,
        string replies,
        INetworkInterfaceLookup lookup,
        IDnsResolver resolver,
        string? controlLocal,
        params ScriptedPendingConnection[] pending)
    {
        diagnostics.ArrangeFtp(Url, replies);
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(replies))
        {
            LocalEndPoint = controlLocal is null ? null : new IPEndPoint(IPAddress.Parse(controlLocal), 60032),
        };
        var connector = new QueuedConnector(ConnectResult.Connected(control));
        var listener = new QueuedListener([.. pending.Select(ListenResult.Listening)]);
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream() },
            mutable => mutable.FtpPort = ftpPort);
        var handler = new FtpProtocolHandler(connector, listener, new QueuedTlsProvider(), resolver, lookup);

        TransferResult result;
        using (diagnostics.Phase("transfer"))
        {
            result = await handler.ExecuteAsync(context);
        }

        diagnostics.ActResult(result);

        return new InterfaceRun(result with { Report = null }, control, listener);
    }

    /// <summary>One active-mode transfer and what it left behind.</summary>
    private sealed record InterfaceRun(TransferResult Result, ScriptedConnection Control, QueuedListener Listener)
    {
        public string Sent => Encoding.Latin1.GetString(Control.Sent);
    }
}
