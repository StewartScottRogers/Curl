using System.Net;
using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that the composition hands <c>--interface</c> and <c>--local-port</c> to the TCP connector as a
/// <see cref="LocalBinding" /> (BL-600), split by the value's prefix as libcurl's <c>bindlocal</c> reads it,
/// and that each connect is dialed bound, through a <see cref="ScriptedTcpDialer" />.
/// </summary>
[TestClass]
public sealed class CurlCompositionLocalBindingTests
{
    [TestMethod]
    public void LocalBindingOf_WithNeitherOption_IsNull() =>
        Assert.IsNull(CurlComposition.LocalBindingOf(Parse("http://h/")));

    [TestMethod]
    [DataRow("eth0", "eth0", "eth0", null)]
    [DataRow("if!eth0", "eth0", null, null)]
    [DataRow("host!127.0.0.1", null, "127.0.0.1", null)]
    [DataRow("ifhost!eth0!127.0.0.1", null, "127.0.0.1", "eth0")]
    public void LocalBindingOf_WithAnInterface_SplitsItByItsPrefix(string value, string? interfaceName, string? hostName, string? deviceName)
    {
        LocalBinding? binding = CurlComposition.LocalBindingOf(Parse("--interface", value, "http://h/"));

        Assert.AreEqual(new LocalBinding(interfaceName, hostName, deviceName, 0, 1), binding);
    }

    [TestMethod]
    public void LocalBindingOf_WithLocalPortsOnly_BindsThePortsAlone() =>
        Assert.AreEqual(
            new LocalBinding(null, null, null, 40000, 11),
            CurlComposition.LocalBindingOf(Parse("--local-port", "40000-40010", "http://h/")));

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

        Assert.IsNotNull(result.Connection);
        Assert.AreEqual((new IPEndPoint(IPAddress.Loopback, 40000), 11), dialer.LocalBinds.Single());
    }

    [TestMethod]
    public void CreateTransports_WithAnInterface_GivesTheTcpConnectorItsBinding()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("--interface", "host!127.0.0.1", "http://h/"));

        Assert.AreEqual(new LocalBinding(null, "127.0.0.1", null, 0, 1), transports.TcpConnector.LocalBinding);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
