using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that the TCP connector the composition builds applies <c>--resolve</c> and
/// <c>--connect-to</c>: each connect is dialed through a <see cref="ScriptedTcpDialer" />,
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
