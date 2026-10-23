using Curl.Engine;
using System.Net;
using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the error line and exit code of a proxy whose name does not resolve, through the production
/// handler set over the connector <see cref="EngineComposition.CreateTcpConnector" /> builds from the
/// command line, whose resolver answers nothing: curl 8.22.0 (Schannel) exits 5 with
/// <c>curl: (5) Could not resolve proxy: &lt;name&gt;</c> (measured on 2026-10-22 with
/// <c>Record-CurlExchange.ps1</c>, BL-3168 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerProxyResolveTests
{
    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("-x")]
    [DataRow("--proxy1.0")]
    public async Task RunAsync_ProxyThatDoesNotResolve_IsExit5CouldNotResolveProxy(string proxyOption)
    {
        // curl -sS -x badproxy.invalid http://x/ -> curl: (5) Could not resolve proxy: badproxy.invalid
        int exitCode = await RunAsync("-sS", proxyOption, "badproxy.invalid", "http://x/");

        string stderr = Encoding.ASCII.GetString(standardError.ToArray());
        Diagnostics.Assert("exit code", (int)CurlExitCode.CouldntResolveProxy, exitCode);
        Diagnostics.Diff("stderr", "curl: (5) Could not resolve proxy: badproxy.invalid" + Environment.NewLine, stderr);
        Assert.AreEqual((int)CurlExitCode.CouldntResolveProxy, exitCode);
        Assert.AreEqual("curl: (5) Could not resolve proxy: badproxy.invalid", stderr.TrimEnd('\r', '\n'));
    }

    private async Task<int> RunAsync(params string[] arguments)
    {
        Diagnostics.Arrange("command line", "curl " + string.Join(" ", arguments));
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        TcpConnector connector = EngineComposition.CreateTcpConnector(
            EasyOptionsFromCommandLine.ToolOptionsOf(parsed.Options), EasyOptionsFromCommandLine.TraceComponentsOf(parsed.Options), EasyOptionsFromCommandLine.NetworkOf(parsed.Options), EasyOptionsFromCommandLine.HttpOf(parsed.Options), EasyOptionsFromCommandLine.ProxyOf(parsed.Options), EasyOptionsFromCommandLine.AuthenticationOf(parsed.Options),
            new UnresolvingDnsResolver(),
            new UnusedTcpDialer(),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);
        InMemoryFileSystem files = new();

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(
                        new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new UnresolvingDnsResolver())),
                        [],
                        loadResolveEntries: connector.LoadResolveEntries),
                    files,
                    files,
                    new MemoryStream(),
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: true)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stderr", standardError.ToArray());

        return exitCode;
    }

    /// <summary>A resolver that answers no address for any name.</summary>
    private sealed class UnresolvingDnsResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([]);
    }

    /// <summary>A dialer no test reaches: a proxy that does not resolve is never dialled.</summary>
    private sealed class UnusedTcpDialer : ITcpDialer
    {
        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A name that did not resolve was dialled.");

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A name that did not resolve was dialled.");

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A name that did not resolve was dialled.");
    }
}
