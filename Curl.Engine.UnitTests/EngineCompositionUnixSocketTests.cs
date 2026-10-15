using Curl.Api.Options.Network;
using Curl.Console;
using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins that the composition hands <c>--unix-socket</c> and <c>--abstract-unix-socket</c> to the TCP
/// connector, gives each option group its own pool, and drops the proxy, as curl 8.21.0 does
/// (measured 2026-09-28, BL-507 Notes). Each connect is dialed through a
/// <see cref="ScriptedTcpDialer" />, which records a Unix socket as a <c>unix:</c> target.
/// </summary>
[TestClass]
public sealed class EngineCompositionUnixSocketTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--unix-socket", "unix:/run/app.sock")]
    [DataRow("--abstract-unix-socket", "unix-abstract:/run/app.sock")]
    public async Task CreateTcpConnector_WithAUnixSocketOption_DialsTheSocketInsteadOfTheHost(string option, string dialed)
    {
        ScriptedConnector server = new([]);
        CommandLineOptions options = Parse(option, "/run/app.sock", "http://example.com:8080/");
        Diagnostics.Arrange("command line arguments", $"{option} /run/app.sock http://example.com:8080/");
        Diagnostics.Arrange("connect target", "example.com:8080");

        ConnectResult result = await CreateConnector(options, server).ConnectAsync(new ConnectTarget("example.com", 8080, false), CancellationToken.None);
        Diagnostics.Act("connected", result.Connection is not null);
        Diagnostics.Act("dialed hosts", string.Join(", ", server.Targets.Select(target => target.Host)));

        Diagnostics.Assert("dialed hosts", dialed, string.Join(", ", server.Targets.Select(target => target.Host)));
        Assert.IsNotNull(result.Connection);
        Assert.HasCount(1, server.Targets);
        Assert.AreEqual(dialed, server.Targets[0].Host);
    }

    [TestMethod]
    public void UnixSocketOf_TakesTheLastSocketOptionOrNone()
    {
        EasyNetworkOptions none = new();
        EasyNetworkOptions abstractSocket = new() { AbstractUnixSocket = "b" };
        EasyNetworkOptions pathSocket = new() { UnixSocketPath = "a" };
        Diagnostics.Arrange("network options", "none | AbstractUnixSocket b | UnixSocketPath a");
        string sockets = string.Join(" | ", new[] { none, abstractSocket, pathSocket }.Select(network => EngineComposition.UnixSocketOf(network)?.ToString() ?? "none"));
        Diagnostics.Act("unix sockets", sockets);
        Diagnostics.Assert("unix sockets", $"none | {new UnixSocketAddress("b", IsAbstract: true)} | {new UnixSocketAddress("a", IsAbstract: false)}", sockets);

        Assert.IsNull(EngineComposition.UnixSocketOf(none));
        Assert.AreEqual(new UnixSocketAddress("b", IsAbstract: true), EngineComposition.UnixSocketOf(abstractSocket));
        Assert.AreEqual(new UnixSocketAddress("a", IsAbstract: false), EngineComposition.UnixSocketOf(pathSocket));
    }

    [TestMethod]
    public void CreateTransports_WithAUnixSocket_GivesTheTcpConnectorThatSocket()
    {
        Diagnostics.Arrange("command line arguments", "--unix-socket /run/app.sock http://h/");
        CurlTransports transports = EngineComposition.CreateTransports(Parse("--unix-socket", "/run/app.sock", "http://h/"), EasyOptionsFromCommandLine.NetworkOf(Parse("--unix-socket", "/run/app.sock", "http://h/")), EasyOptionsFromCommandLine.TlsOf(Parse("--unix-socket", "/run/app.sock", "http://h/")), EasyOptionsFromCommandLine.HttpOf(Parse("--unix-socket", "/run/app.sock", "http://h/")), EasyOptionsFromCommandLine.ProxyOf(Parse("--unix-socket", "/run/app.sock", "http://h/")));
        Diagnostics.Act("tcp connector unix socket", transports.TcpConnector.UnixSocket);

        Diagnostics.Assert("tcp connector unix socket", new UnixSocketAddress("/run/app.sock", IsAbstract: false), transports.TcpConnector.UnixSocket);
        Assert.AreEqual(new UnixSocketAddress("/run/app.sock", IsAbstract: false), transports.TcpConnector.UnixSocket);
    }

    [TestMethod]
    public async Task OptionGroupsWithDifferentSockets_NeverShareAPooledConnection()
    {
        // Each group builds its own transports, so a connection to one group's socket, left idle
        // for reuse, is never handed to a transfer of a group with another socket.
        CommandLineParseResult parsed = CommandLineParser.Parse(
            ["--unix-socket", "/run/a.sock", "http://h/", "--next", "--unix-socket", "/run/b.sock", "http://h/"], _ => true);
        Diagnostics.Arrange("command line arguments", "--unix-socket /run/a.sock http://h/ --next --unix-socket /run/b.sock http://h/");
        Assert.IsTrue(parsed.IsAccepted);
        ScriptedConnector server = new([]);
        ConnectTarget target = new("h", 80, false) { PoolScheme = "http" };
        await using PoolingConnector firstGroupPool = new(CreateConnector(parsed.Groups[0], server), TimeProvider.System);
        await using PoolingConnector secondGroupPool = new(CreateConnector(parsed.Groups[1], server), TimeProvider.System);

        ConnectResult first = await firstGroupPool.ConnectAsync(target, CancellationToken.None);
        first.Connection!.MarkReusable();
        await first.Connection.DisposeAsync();
        ConnectResult second = await secondGroupPool.ConnectAsync(target, CancellationToken.None);
        Diagnostics.Act("second connection reused", second.IsReused);
        Diagnostics.Act("dialed hosts", string.Join(", ", server.Targets.Select(dialed => dialed.Host)));

        Diagnostics.Assert("second connection reused", false, second.IsReused);
        Diagnostics.Assert("dialed hosts", "unix:/run/a.sock, unix:/run/b.sock", string.Join(", ", server.Targets.Select(dialed => dialed.Host)));
        Assert.IsFalse(second.IsReused);
        CollectionAssert.AreEqual(new[] { "unix:/run/a.sock", "unix:/run/b.sock" }, server.Targets.Select(dialed => dialed.Host).ToArray());
        Assert.AreNotSame(EngineComposition.CreateTransports(parsed.Groups[0], EasyOptionsFromCommandLine.NetworkOf(parsed.Groups[0]), EasyOptionsFromCommandLine.TlsOf(parsed.Groups[0]), EasyOptionsFromCommandLine.HttpOf(parsed.Groups[0]), EasyOptionsFromCommandLine.ProxyOf(parsed.Groups[0])).PoolingConnector, EngineComposition.CreateTransports(parsed.Groups[1], EasyOptionsFromCommandLine.NetworkOf(parsed.Groups[1]), EasyOptionsFromCommandLine.TlsOf(parsed.Groups[1]), EasyOptionsFromCommandLine.HttpOf(parsed.Groups[1]), EasyOptionsFromCommandLine.ProxyOf(parsed.Groups[1])).PoolingConnector);
    }

    [TestMethod]
    public void TransferProxySelection_WithAUnixSocket_DropsEvenAnUnparsableProxy()
    {
        // curl --unix-socket nosuch.sock -x "http://[bad" http://localhost/ -> exit 7 over unix://nosuch.sock,
        // where without --unix-socket it is exit 5 Unsupported proxy syntax (curl 8.21.0, measured).
        CommandLineOptions options = Parse("--unix-socket", "nosuch.sock", "-x", "http://[bad", "http://localhost/");
        Diagnostics.Arrange("command line arguments", "--unix-socket nosuch.sock -x http://[bad http://localhost/");

        bool selected = TransferProxySelection.TrySelect(new ProxySelector(_ => null), EasyOptionsFromCommandLine.ProxyOf(options), options.UnixSocketPath is not null, options.ProxyCredentials, CurlUrl.Parse("http://localhost/"), out ProxyEndpoint? proxy, out TransferResult? failure);
        Diagnostics.Act("selected", selected);
        Diagnostics.Act("proxy", proxy?.ToString() ?? "none");
        Diagnostics.Act("failure", failure?.ExitCode.ToString() ?? "none");

        Diagnostics.Assert("selected, proxy, failure", "True, none, none", $"{selected}, {proxy?.ToString() ?? "none"}, {failure?.ExitCode.ToString() ?? "none"}");
        Assert.IsTrue(selected);
        Assert.IsNull(proxy);
        Assert.IsNull(failure);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }

    private static TcpConnector CreateConnector(CommandLineOptions options, ScriptedConnector server) =>
        EngineComposition.CreateTcpConnector(
            options, EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options),
            new LoopbackDnsResolver(),
            new ScriptedTcpDialer(server),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);
}
