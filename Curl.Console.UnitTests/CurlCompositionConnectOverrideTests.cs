using System.Net;

using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that the TCP and UDP connectors the composition builds apply <c>--resolve</c> and
/// <c>--connect-to</c>: each TCP connect is dialed through a <see cref="ScriptedTcpDialer" />,
/// whose <see cref="ScriptedConnector" /> records the address and port actually dialed.
/// </summary>
[TestClass]
public sealed class CurlCompositionConnectOverrideTests
{
    [TestMethod]
    public async Task CreateTcpConnector_WithResolve_DialsTheOverriddenAddress()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, "example.com", 80, "--resolve", "example.com:80:192.0.2.7");

        Assert.IsNotNull(result.Connection);
        Assert.HasCount(1, server.Targets);
        Assert.AreEqual("192.0.2.7", server.Targets[0].Host);
        Assert.AreEqual(80, server.Targets[0].Port);
    }

    [TestMethod]
    public async Task CreateTcpConnector_WithConnectTo_DialsTheMappedPort()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, "example.com", 80, "--connect-to", "example.com:80:mapped.test:8080");

        Assert.IsNotNull(result.Connection);
        Assert.HasCount(1, server.Targets);
        Assert.AreEqual("127.0.0.1", server.Targets[0].Host);
        Assert.AreEqual(8080, server.Targets[0].Port);
    }

    [TestMethod]
    public async Task CreateTcpConnector_WithConnectToAndResolveForTheMappedHost_DialsTheMappedHostsOverriddenAddress()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(
            server,
            "example.com",
            80,
            "--connect-to",
            "example.com:80:mapped.test:8080",
            "--resolve",
            "mapped.test:8080:192.0.2.9",
            "--resolve",
            "example.com:80:192.0.2.1");

        Assert.IsNotNull(result.Connection);
        Assert.HasCount(1, server.Targets);
        Assert.AreEqual("192.0.2.9", server.Targets[0].Host);
        Assert.AreEqual(8080, server.Targets[0].Port);
    }

    [TestMethod]
    public async Task CreateTcpConnector_WithoutResolveOrConnectTo_DialsTheResolvedAddressAtTheUrlPort()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, "example.com", 80);

        Assert.IsNotNull(result.Connection);
        Assert.AreEqual("127.0.0.1", server.Targets[0].Host);
        Assert.AreEqual(80, server.Targets[0].Port);
    }

    [TestMethod]
    public async Task CreateTcpConnector_WithUnparsableResolve_FailsWithExit49AndDialsNothing()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, "example.com", 80, "--resolve", "example.com:80:bad");

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual("Could not parse CURLOPT_RESOLVE entry 'example.com:80:bad'", result.ErrorMessage);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public async Task CreateTcpConnector_WithUnparsableConnectToPort_FailsWithExit49AndDialsNothing()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, "example.com", 80, "--connect-to", "example.com:80:mapped.test:x");

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual("No valid port number in 'mapped.test:x'", result.ErrorMessage);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public async Task CreateTransports_WithConnectToAndResolve_UdpConnectorOpensAtTheMappedHostsOverriddenAddress()
    {
        // curl -v --connect-to tftp.test:6969:other: --resolve other:6969:127.0.0.1 tftp://tftp.test:6969/x:
        // "Trying 127.0.0.1:6969..." (curl 8.21.0, 2026-09-27).
        DatagramOpenResult result = await OpenTftpAsync(
            "--connect-to", "tftp.test:69:mapped.test:7000", "--resolve", "mapped.test:7000:127.0.0.1");

        Assert.IsNotNull(result.Channel);
        await using IDatagramChannel channel = result.Channel;
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 7000), channel.ServerEndPoint);
    }

    [TestMethod]
    public async Task CreateTransports_WithUnparsableResolve_UdpConnectorFailsWithExit49()
    {
        DatagramOpenResult result = await OpenTftpAsync("--resolve", "bad");

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual("Could not parse CURLOPT_RESOLVE entry 'bad'", result.ErrorMessage);
    }

    [TestMethod]
    public async Task CreateTransports_WithUnparsableConnectToPort_UdpConnectorFailsWithExit49()
    {
        DatagramOpenResult result = await OpenTftpAsync("--connect-to", "tftp.test:69:mapped.test:x");

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual("No valid port number in 'mapped.test:x'", result.ErrorMessage);
    }

    /// <summary>
    /// Parses <paramref name="arguments" /> and a <c>tftp://tftp.test/</c> URL, builds the
    /// run's transports from them, and opens the UDP connector to <c>tftp.test</c> on port 69.
    /// </summary>
    private static async Task<DatagramOpenResult> OpenTftpAsync(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "tftp://tftp.test/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);

        CurlTransports transports = CurlComposition.CreateTransports(parsed.Options);

        return await transports.UdpDatagramConnector.OpenAsync("tftp.test", 69, CancellationToken.None);
    }

    /// <summary>
    /// Parses <paramref name="arguments" /> and a URL, builds the production TCP connector from
    /// them over a <see cref="LoopbackDnsResolver" /> and <paramref name="server" />, and connects
    /// to <paramref name="host" /> on <paramref name="port" />.
    /// </summary>
    private static async Task<ConnectResult> ConnectAsync(ScriptedConnector server, string host, int port, params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, $"http://{host}:{port}/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);

        TcpConnector connector = CurlComposition.CreateTcpConnector(
            parsed.Options,
            new LoopbackDnsResolver(),
            new ScriptedTcpDialer(server),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);

        return await connector.ConnectAsync(new ConnectTarget(host, port, false), CancellationToken.None);
    }
}
