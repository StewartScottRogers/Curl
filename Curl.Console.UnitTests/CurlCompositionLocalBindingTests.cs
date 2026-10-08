using System.Net;
using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;

using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that the composition hands <c>--interface</c> and <c>--local-port</c> to the TCP connector as a
/// <see cref="LocalBinding" /> (BL-600), split by the value's prefix as libcurl's <c>bindlocal</c> reads it,
/// and that each connect is dialed bound, through a <see cref="ScriptedTcpDialer" />.
/// </summary>
[TestClass]
public sealed class CurlCompositionLocalBindingTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void LocalBindingOf_WithNeitherOption_IsNull()
    {
        LocalBinding? binding = LocalBindingOf("http://h/");

        Diagnostics.Assert("binding", null, binding);
        Assert.IsNull(binding);
    }

    [TestMethod]
    [DataRow("eth0", "eth0", "eth0", null)]
    [DataRow("if!eth0", "eth0", null, null)]
    [DataRow("host!127.0.0.1", null, "127.0.0.1", null)]
    [DataRow("ifhost!eth0!127.0.0.1", null, "127.0.0.1", "eth0")]
    public void LocalBindingOf_WithAnInterface_SplitsItByItsPrefix(string value, string? interfaceName, string? hostName, string? deviceName)
    {
        LocalBinding? binding = LocalBindingOf("--interface", value, "http://h/");

        LocalBinding expected = new(interfaceName, hostName, deviceName, 0, 1);
        Diagnostics.Assert("binding", expected, binding);
        Assert.AreEqual(expected, binding);
    }

    [TestMethod]
    public void LocalBindingOf_WithLocalPortsOnly_BindsThePortsAlone()
    {
        LocalBinding? binding = LocalBindingOf("--local-port", "40000-40010", "http://h/");

        LocalBinding expected = new(null, null, null, 40000, 11);
        Diagnostics.Assert("binding", expected, binding);
        Assert.AreEqual(expected, binding);
    }

    [TestMethod]
    public async Task CreateTcpConnector_WithInterfaceAndLocalPort_DialsFromThatAddressAndRange()
    {
        // curl --interface 127.0.0.1 --local-port 40000-40010 -w '%{local_port}' -> 40000, exit 0.
        ScriptedConnector server = new([]);
        ScriptedTcpDialer dialer = new(server);
        TcpConnector connector = CurlComposition.CreateTcpConnector(
            Parse("--interface", "127.0.0.1", "--local-port", "40000-40010", "http://127.0.0.1:47599/"),
            new LoopbackDnsResolver(),
            dialer,
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47599, false), CancellationToken.None);
        Diagnostics.Act("connected", result.Connection is not null);
        Diagnostics.Act("local binds", string.Join("; ", dialer.LocalBinds));

        Diagnostics.Assert("local bind", (new IPEndPoint(IPAddress.Loopback, 40000), 11), dialer.LocalBinds.Single());
        Assert.IsNotNull(result.Connection);
        Assert.AreEqual((new IPEndPoint(IPAddress.Loopback, 40000), 11), dialer.LocalBinds.Single());
    }

    [TestMethod]
    public void CreateTransports_WithAnInterface_GivesTheTcpConnectorItsBinding()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("--interface", "host!127.0.0.1", "http://h/"));
        Diagnostics.Act("TCP connector binding", transports.TcpConnector.LocalBinding);

        LocalBinding expected = new(null, "127.0.0.1", null, 0, 1);
        Diagnostics.Assert("TCP connector binding", expected, transports.TcpConnector.LocalBinding);
        Assert.AreEqual(expected, transports.TcpConnector.LocalBinding);
    }

    private LocalBinding? LocalBindingOf(params string[] arguments)
    {
        LocalBinding? binding = CurlComposition.LocalBindingOf(Parse(arguments));
        Diagnostics.Act("binding", binding);
        return binding;
    }

    private CommandLineOptions Parse(params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
