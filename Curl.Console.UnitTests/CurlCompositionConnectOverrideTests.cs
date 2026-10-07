using System.Net;

using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that the TCP and UDP connectors the composition builds apply <c>--resolve</c> and
/// <c>--connect-to</c>: each TCP connect is dialed through a <see cref="ScriptedTcpDialer" />,
/// whose <see cref="ScriptedConnector" /> records the address and port actually dialed.
/// </summary>
[TestClass]
public sealed class CurlCompositionConnectOverrideTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task CreateTcpConnector_WithResolve_DialsTheOverriddenAddress()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, "example.com", 80, "--resolve", "example.com:80:192.0.2.7");

        Diagnostics.Assert("result.Connection is not null", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection);
        Diagnostics.Assert("server.Targets count", 1, server.Targets.Count);
        Assert.HasCount(1, server.Targets);
        Diagnostics.Assert("server.Targets[0].Host", "192.0.2.7", server.Targets[0].Host);
        Assert.AreEqual("192.0.2.7", server.Targets[0].Host);
        Diagnostics.Assert("server.Targets[0].Port", 80, server.Targets[0].Port);
        Assert.AreEqual(80, server.Targets[0].Port);
    }

    [TestMethod]
    public async Task CreateTcpConnector_WithConnectTo_DialsTheMappedPort()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, "example.com", 80, "--connect-to", "example.com:80:mapped.test:8080");

        Diagnostics.Assert("result.Connection is not null", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection);
        Diagnostics.Assert("server.Targets count", 1, server.Targets.Count);
        Assert.HasCount(1, server.Targets);
        Diagnostics.Assert("server.Targets[0].Host", "127.0.0.1", server.Targets[0].Host);
        Assert.AreEqual("127.0.0.1", server.Targets[0].Host);
        Diagnostics.Assert("server.Targets[0].Port", 8080, server.Targets[0].Port);
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

        Diagnostics.Assert("result.Connection is not null", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection);
        Diagnostics.Assert("server.Targets count", 1, server.Targets.Count);
        Assert.HasCount(1, server.Targets);
        Diagnostics.Assert("server.Targets[0].Host", "192.0.2.9", server.Targets[0].Host);
        Assert.AreEqual("192.0.2.9", server.Targets[0].Host);
        Diagnostics.Assert("server.Targets[0].Port", 8080, server.Targets[0].Port);
        Assert.AreEqual(8080, server.Targets[0].Port);
    }

    [TestMethod]
    public async Task CreateTcpConnector_WithoutResolveOrConnectTo_DialsTheResolvedAddressAtTheUrlPort()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, "example.com", 80);

        Diagnostics.Assert("result.Connection is not null", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection);
        Diagnostics.Assert("server.Targets[0].Host", "127.0.0.1", server.Targets[0].Host);
        Assert.AreEqual("127.0.0.1", server.Targets[0].Host);
        Diagnostics.Assert("server.Targets[0].Port", 80, server.Targets[0].Port);
        Assert.AreEqual(80, server.Targets[0].Port);
    }

    [TestMethod]
    public async Task CreateTcpConnector_WithUnparsableResolve_FailsWithExit49AndDialsNothing()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, "example.com", 80, "--resolve", "example.com:80:bad");

        Diagnostics.Assert("result.ExitCode", CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Diagnostics.Assert("result.ErrorMessage", "Could not parse CURLOPT_RESOLVE entry 'example.com:80:bad'", result.ErrorMessage);
        Assert.AreEqual("Could not parse CURLOPT_RESOLVE entry 'example.com:80:bad'", result.ErrorMessage);
        Diagnostics.Assert("server.Targets count", 0, server.Targets.Count);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public async Task CreateTcpConnector_WithUnparsableConnectToPort_FailsWithExit49AndDialsNothing()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, "example.com", 80, "--connect-to", "example.com:80:mapped.test:x");

        Diagnostics.Assert("result.ExitCode", CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Diagnostics.Assert("result.ErrorMessage", "No valid port number in 'mapped.test:x'", result.ErrorMessage);
        Assert.AreEqual("No valid port number in 'mapped.test:x'", result.ErrorMessage);
        Diagnostics.Assert("server.Targets count", 0, server.Targets.Count);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public async Task CreateTransports_WithConnectToAndResolve_UdpConnectorOpensAtTheMappedHostsOverriddenAddress()
    {
        // curl -v --connect-to tftp.test:6969:other: --resolve other:6969:127.0.0.1 tftp://tftp.test:6969/x:
        // "Trying 127.0.0.1:6969..." (curl 8.21.0, 2026-09-27).
        DatagramOpenResult result = await OpenTftpAsync(
            "--connect-to", "tftp.test:69:mapped.test:7000", "--resolve", "mapped.test:7000:127.0.0.1");

        Diagnostics.Assert("result.Channel is not null", true, result.Channel is not null);
        Assert.IsNotNull(result.Channel);
        await using IDatagramChannel channel = result.Channel;
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 7000), channel.ServerEndPoint);
    }

    [TestMethod]
    public async Task CreateTransports_WithUnparsableResolve_UdpConnectorFailsWithExit49()
    {
        DatagramOpenResult result = await OpenTftpAsync("--resolve", "bad");

        Diagnostics.Assert("result.ExitCode", CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Diagnostics.Assert("result.ErrorMessage", "Could not parse CURLOPT_RESOLVE entry 'bad'", result.ErrorMessage);
        Assert.AreEqual("Could not parse CURLOPT_RESOLVE entry 'bad'", result.ErrorMessage);
    }

    [TestMethod]
    public async Task CreateTransports_WithUnparsableConnectToPort_UdpConnectorFailsWithExit49()
    {
        DatagramOpenResult result = await OpenTftpAsync("--connect-to", "tftp.test:69:mapped.test:x");

        Diagnostics.Assert("result.ExitCode", CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Diagnostics.Assert("result.ErrorMessage", "No valid port number in 'mapped.test:x'", result.ErrorMessage);
        Assert.AreEqual("No valid port number in 'mapped.test:x'", result.ErrorMessage);
    }

    /// <summary>
    /// Parses <paramref name="arguments" /> and a <c>tftp://tftp.test/</c> URL, builds the
    /// run's transports from them, and opens the UDP connector to <c>tftp.test</c> on port 69.
    /// </summary>
    private async Task<DatagramOpenResult> OpenTftpAsync(params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments.Append("tftp://tftp.test/")));
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "tftp://tftp.test/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);

        CurlTransports transports = CurlComposition.CreateTransports(parsed.Options);

        DatagramOpenResult result;
        using (Diagnostics.Phase("open"))
        {
            result = await transports.UdpDatagramConnector.OpenAsync("tftp.test", 69, CancellationToken.None);
        }

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Act("server end point", result.Channel?.ServerEndPoint);
        return result;
    }

    /// <summary>
    /// Parses <paramref name="arguments" /> and a URL, builds the production TCP connector from
    /// them over a <see cref="LoopbackDnsResolver" /> and <paramref name="server" />, and connects
    /// to <paramref name="host" /> on <paramref name="port" />.
    /// </summary>
    private async Task<ConnectResult> ConnectAsync(ScriptedConnector server, string host, int port, params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments.Append($"http://{host}:{port}/")));
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, $"http://{host}:{port}/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);

        TcpConnector connector = CurlComposition.CreateTcpConnector(
            parsed.Options,
            new LoopbackDnsResolver(),
            new ScriptedTcpDialer(server),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);

        ConnectResult result;
        using (Diagnostics.Phase("connect"))
        {
            result = await connector.ConnectAsync(new ConnectTarget(host, port, false), CancellationToken.None);
        }

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Act("dialled targets", string.Join(", ", server.Targets.Select(target => $"{target.Host}:{target.Port}")));
        return result;
    }
}
