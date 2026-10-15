using Curl.Console;
using System.Net.Sockets;
using Curl.Api.Options.Network;

using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins that <c>-4</c> and <c>-6</c> on the command line reach the TCP and UDP connectors the
/// composition builds, so a name is dialled at the chosen family's addresses only (BL-500).
/// </summary>
[TestClass]
public sealed class EngineCompositionAddressFamilyTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(IpResolve.V4, AddressFamily.InterNetwork)]
    [DataRow(IpResolve.V6, AddressFamily.InterNetworkV6)]
    [DataRow(IpResolve.Whatever, AddressFamily.Unspecified)]
    public void AddressFamilyOf_MapsTheChosenFamily(IpResolve ipResolve, AddressFamily expected)
    {
        Diagnostics.Arrange("IP resolve", ipResolve);
        EasyNetworkOptions network = new() { IpResolve = ipResolve };
        Diagnostics.Act("address family", EngineComposition.AddressFamilyOf(network));

        Diagnostics.Assert("address family", expected, EngineComposition.AddressFamilyOf(network));
        Assert.AreEqual(expected, EngineComposition.AddressFamilyOf(network));
    }

    [TestMethod]
    public async Task CreateTcpConnector_UnderIPv4WithAResolveEntryOfBothFamilies_DialsOnlyTheIPv4Address()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, "-4", "--resolve", "example.com:80:[::1],192.0.2.7");

        Diagnostics.Assert("result.Connection is not null", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection);
        Diagnostics.Assert("server.Targets count", 1, server.Targets.Count);
        Assert.HasCount(1, server.Targets);
        Diagnostics.Assert("server.Targets[0].Host", "192.0.2.7", server.Targets[0].Host);
        Assert.AreEqual("192.0.2.7", server.Targets[0].Host);
    }

    [TestMethod]
    public async Task CreateTcpConnector_UnderIPv6WithAResolveEntryOfOnlyIPv4_FailsWithExit6AndDialsNothing()
    {
        // curl -6 --resolve foo:47500:127.0.0.1 http://foo:47500/ -> curl: (6) Could not resolve host: foo
        // (curl 8.21.0, 2026-09-28).
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, "-6", "--resolve", "example.com:80:192.0.2.7");

        Diagnostics.Assert("result.ExitCode", CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Diagnostics.Assert("result.ErrorMessage", "Could not resolve host: example.com", result.ErrorMessage);
        Assert.AreEqual("Could not resolve host: example.com", result.ErrorMessage);
        Diagnostics.Assert("server.Targets count", 0, server.Targets.Count);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public async Task CreateTransports_UnderIPv6WithAResolveEntryOfOnlyIPv4_UdpConnectorFailsWithExit6()
    {
        Diagnostics.Arrange("arguments", "-6 --resolve tftp.test:69:127.0.0.1 tftp://tftp.test/");
        CommandLineParseResult parsed = CommandLineParser.Parse(["-6", "--resolve", "tftp.test:69:127.0.0.1", "tftp://tftp.test/"], _ => true);
        CurlTransports transports = EngineComposition.CreateTransports(parsed.Options!, EasyOptionsFromCommandLine.NetworkOf(parsed.Options!), EasyOptionsFromCommandLine.TlsOf(parsed.Options!), EasyOptionsFromCommandLine.HttpOf(parsed.Options!), EasyOptionsFromCommandLine.ProxyOf(parsed.Options!), EasyOptionsFromCommandLine.AuthenticationOf(parsed.Options!));

        DatagramOpenResult result;
        using (Diagnostics.Phase("open"))
        {
            result = await transports.UdpDatagramConnector.OpenAsync("tftp.test", 69, CancellationToken.None);
        }

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);

        Diagnostics.Assert("result.ExitCode", CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Diagnostics.Assert("result.ErrorMessage", "Could not resolve host: tftp.test", result.ErrorMessage);
        Assert.AreEqual("Could not resolve host: tftp.test", result.ErrorMessage);
    }

    private async Task<ConnectResult> ConnectAsync(ScriptedConnector server, params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments.Append("http://example.com/")));
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://example.com/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);

        TcpConnector connector = EngineComposition.CreateTcpConnector(
            parsed.Options, EasyOptionsFromCommandLine.NetworkOf(parsed.Options), EasyOptionsFromCommandLine.HttpOf(parsed.Options), EasyOptionsFromCommandLine.ProxyOf(parsed.Options), EasyOptionsFromCommandLine.AuthenticationOf(parsed.Options),
            new LoopbackDnsResolver(),
            new ScriptedTcpDialer(server),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);

        ConnectResult result;
        using (Diagnostics.Phase("connect"))
        {
            result = await connector.ConnectAsync(new ConnectTarget("example.com", 80, false), CancellationToken.None);
        }

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Act("dialled targets", string.Join(", ", server.Targets.Select(target => $"{target.Host}:{target.Port}")));
        return result;
    }
}
