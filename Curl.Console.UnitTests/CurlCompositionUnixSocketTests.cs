using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that the composition hands <c>--unix-socket</c> and <c>--abstract-unix-socket</c> to the TCP
/// connector, gives each option group its own pool, and drops the proxy, as curl 8.21.0 does
/// (measured 2026-09-28, BL-507 Notes). Each connect is dialed through a
/// <see cref="ScriptedTcpDialer" />, which records a Unix socket as a <c>unix:</c> target.
/// </summary>
[TestClass]
public sealed class CurlCompositionUnixSocketTests
{
    [TestMethod]
    [DataRow("--unix-socket", "unix:/run/app.sock")]
    [DataRow("--abstract-unix-socket", "unix-abstract:/run/app.sock")]
    public async Task CreateTcpConnector_WithAUnixSocketOption_DialsTheSocketInsteadOfTheHost(string option, string dialed)
    {
        ScriptedConnector server = new([]);
        CommandLineOptions options = Parse(option, "/run/app.sock", "http://example.com:8080/");

        ConnectResult result = await CreateConnector(options, server).ConnectAsync(new ConnectTarget("example.com", 8080, false), CancellationToken.None);

        Assert.IsNotNull(result.Connection);
        Assert.HasCount(1, server.Targets);
        Assert.AreEqual(dialed, server.Targets[0].Host);
    }

    [TestMethod]
    public void UnixSocketOf_TakesTheLastSocketOptionOrNone()
    {
        Assert.IsNull(CurlComposition.UnixSocketOf(Parse("http://h/")));
        Assert.AreEqual(new UnixSocketAddress("b", IsAbstract: true), CurlComposition.UnixSocketOf(Parse("--unix-socket", "a", "--abstract-unix-socket", "b", "http://h/")));
        Assert.AreEqual(new UnixSocketAddress("a", IsAbstract: false), CurlComposition.UnixSocketOf(Parse("--unix-socket", "a", "http://h/")));
    }

    [TestMethod]
    public void CreateTransports_WithAUnixSocket_GivesTheTcpConnectorThatSocket()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("--unix-socket", "/run/app.sock", "http://h/"));

        Assert.AreEqual(new UnixSocketAddress("/run/app.sock", IsAbstract: false), transports.TcpConnector.UnixSocket);
    }

    [TestMethod]
    public async Task OptionGroupsWithDifferentSockets_NeverShareAPooledConnection()
    {
        // Each group builds its own transports, so a connection to one group's socket, left idle
        // for reuse, is never handed to a transfer of a group with another socket.
        CommandLineParseResult parsed = CommandLineParser.Parse(
            ["--unix-socket", "/run/a.sock", "http://h/", "--next", "--unix-socket", "/run/b.sock", "http://h/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        ScriptedConnector server = new([]);
        ConnectTarget target = new("h", 80, false) { PoolScheme = "http" };
        await using PoolingConnector firstGroupPool = new(CreateConnector(parsed.Groups[0], server), TimeProvider.System);
        await using PoolingConnector secondGroupPool = new(CreateConnector(parsed.Groups[1], server), TimeProvider.System);

        ConnectResult first = await firstGroupPool.ConnectAsync(target, CancellationToken.None);
        first.Connection!.MarkReusable();
        await first.Connection.DisposeAsync();
        ConnectResult second = await secondGroupPool.ConnectAsync(target, CancellationToken.None);

        Assert.IsFalse(second.IsReused);
        CollectionAssert.AreEqual(new[] { "unix:/run/a.sock", "unix:/run/b.sock" }, server.Targets.Select(dialed => dialed.Host).ToArray());
        Assert.AreNotSame(CurlComposition.CreateTransports(parsed.Groups[0]).PoolingConnector, CurlComposition.CreateTransports(parsed.Groups[1]).PoolingConnector);
    }

    [TestMethod]
    public void TransferProxySelection_WithAUnixSocket_DropsEvenAnUnparsableProxy()
    {
        // curl --unix-socket nosuch.sock -x "http://[bad" http://localhost/ -> exit 7 over unix://nosuch.sock,
        // where without --unix-socket it is exit 5 Unsupported proxy syntax (curl 8.21.0, measured).
        CommandLineOptions options = Parse("--unix-socket", "nosuch.sock", "-x", "http://[bad", "http://localhost/");

        bool selected = TransferProxySelection.TrySelect(new ProxySelector(_ => null), options, CurlUrl.Parse("http://localhost/"), out ProxyEndpoint? proxy, out TransferResult? failure);

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
        CurlComposition.CreateTcpConnector(
            options,
            new LoopbackDnsResolver(),
            new ScriptedTcpDialer(server),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);
}
