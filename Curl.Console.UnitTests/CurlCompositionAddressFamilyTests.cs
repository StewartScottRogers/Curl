using System.Net.Sockets;

using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that <c>-4</c> and <c>-6</c> on the command line reach the TCP and UDP connectors the
/// composition builds, so a name is dialled at the chosen family's addresses only (BL-500).
/// </summary>
[TestClass]
public sealed class CurlCompositionAddressFamilyTests
{
    [TestMethod]
    [DataRow("-4", AddressFamily.InterNetwork)]
    [DataRow("--ipv6", AddressFamily.InterNetworkV6)]
    [DataRow("-v", AddressFamily.Unspecified)]
    public void AddressFamilyOf_MapsTheChosenFamily(string option, AddressFamily expected)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([option, "http://example.com/"], _ => true);

        Assert.AreEqual(expected, CurlComposition.AddressFamilyOf(parsed.Options!));
    }

    [TestMethod]
    public async Task CreateTcpConnector_UnderIPv4WithAResolveEntryOfBothFamilies_DialsOnlyTheIPv4Address()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, "-4", "--resolve", "example.com:80:[::1],192.0.2.7");

        Assert.IsNotNull(result.Connection);
        Assert.HasCount(1, server.Targets);
        Assert.AreEqual("192.0.2.7", server.Targets[0].Host);
    }

    [TestMethod]
    public async Task CreateTcpConnector_UnderIPv6WithAResolveEntryOfOnlyIPv4_FailsWithExit6AndDialsNothing()
    {
        // curl -6 --resolve foo:47500:127.0.0.1 http://foo:47500/ -> curl: (6) Could not resolve host: foo
        // (curl 8.21.0, 2026-09-28).
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, "-6", "--resolve", "example.com:80:192.0.2.7");

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: example.com", result.ErrorMessage);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public async Task CreateTransports_UnderIPv6WithAResolveEntryOfOnlyIPv4_UdpConnectorFailsWithExit6()
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(["-6", "--resolve", "tftp.test:69:127.0.0.1", "tftp://tftp.test/"], _ => true);
        CurlTransports transports = CurlComposition.CreateTransports(parsed.Options!);

        DatagramOpenResult result = await transports.UdpDatagramConnector.OpenAsync("tftp.test", 69, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: tftp.test", result.ErrorMessage);
    }

    private static async Task<ConnectResult> ConnectAsync(ScriptedConnector server, params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://example.com/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);

        TcpConnector connector = CurlComposition.CreateTcpConnector(
            parsed.Options,
            new LoopbackDnsResolver(),
            new ScriptedTcpDialer(server),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);

        return await connector.ConnectAsync(new ConnectTarget("example.com", 80, false), CancellationToken.None);
    }
}
