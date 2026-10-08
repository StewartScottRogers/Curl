using System.Net;
using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that <c>--connect-timeout</c>, and a smaller <c>-m</c>, end a connect for a scheme with no
/// timeout of its own (ADR-0117), against curl 8.21.0 (mingw, Schannel) measured on 2026-09-28 with
/// <c>Record-CurlExchange.ps1</c> (BL-510 Notes): <c>-v --connect-timeout 1
/// dict://10.255.255.1/d:x</c> printed <c>*   Trying 10.255.255.1:2628...</c>, <c>* Connection
/// timed out after 1010 milliseconds</c>, <c>* closing connection #0</c> and <c>curl: (28)
/// Connection timed out after 1010 milliseconds</c>, exit 28. The transfer runs through the
/// production handler set over the connector <see cref="CurlComposition.CreateTcpConnector" />
/// builds, dialing a <see cref="StallingTcpDialer" /> on an <see cref="ImmediateTimerTimeProvider" />.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerConnectTimeoutTests
{
    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.ASCII.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_DictToAStalledDialWithConnectTimeout_EndsWithExit28AndCurlsMessage()
    {
        // The -v connect lines wait on the dict handler passing its events to the target (BL-774).
        int exitCode = await RunAsync("--connect-timeout", "1", "dict://127.0.0.1/d:x");

        Diagnostics.Assert("exit code", (int)CurlExitCode.OperationTimedOut, exitCode);
        Diagnostics.Assert(
            "stderr",
            "curl: (28) Connection timed out after 1000 milliseconds<NewLine>",
            StandardErrorText.Replace(Environment.NewLine, "<NewLine>", StringComparison.Ordinal));
        Assert.AreEqual((int)CurlExitCode.OperationTimedOut, exitCode);
        Assert.AreEqual("curl: (28) Connection timed out after 1000 milliseconds" + Environment.NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DictToAStalledDialWithMaxTimeBelowConnectTimeout_EndsAtTheMaxTime()
    {
        // curl -v --connect-timeout 5 -m 1 http://10.255.255.1/ -> curl: (28) Connection timed out after 1000 milliseconds.
        int exitCode = await RunAsync("--connect-timeout", "5", "-m", "1", "dict://127.0.0.1/d:x");

        Diagnostics.Assert("exit code", (int)CurlExitCode.OperationTimedOut, exitCode);
        Diagnostics.Assert(
            "stderr ends with the timeout message",
            true,
            StandardErrorText.EndsWith("curl: (28) Connection timed out after 1000 milliseconds" + Environment.NewLine, StringComparison.Ordinal));
        Assert.AreEqual((int)CurlExitCode.OperationTimedOut, exitCode);
        StringAssert.EndsWith(StandardErrorText, "curl: (28) Connection timed out after 1000 milliseconds" + Environment.NewLine);
    }

    [TestMethod]
    [DataRow(null, null, 300_000, DisplayName = "neither")]
    [DataRow("0", null, 300_000, DisplayName = "--connect-timeout 0")]
    [DataRow("2", null, 2000, DisplayName = "--connect-timeout 2")]
    [DataRow("2", "1", 1000, DisplayName = "-m below")]
    [DataRow("2", "3", 2000, DisplayName = "-m above")]
    [DataRow("2", "0", 2000, DisplayName = "-m 0")]
    [DataRow(null, "400", 300_000, DisplayName = "-m above the default")]
    public void ConnectTimeoutOf_TakesTheConnectTimeoutOrASmallerMaxTime(string? connectTimeout, string? maxTime, int expectedMilliseconds)
    {
        List<string> arguments = [];
        if (connectTimeout is not null)
        {
            arguments.AddRange(["--connect-timeout", connectTimeout]);
        }

        if (maxTime is not null)
        {
            arguments.AddRange(["-m", maxTime]);
        }

        Diagnostics.Arrange("command line", string.Join(" ", [.. arguments, "dict://example.com/"]));

        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "dict://example.com/"], _ => true);
        TimeSpan actual;
        using (Diagnostics.Phase("connect timeout of"))
        {
            actual = CurlComposition.ConnectTimeoutOf(parsed.Options!);
        }

        Diagnostics.Act("connect timeout", actual);
        Diagnostics.Assert("connect timeout", TimeSpan.FromMilliseconds(expectedMilliseconds), actual);
        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), CurlComposition.ConnectTimeoutOf(parsed.Options!));
    }

    /// <summary>
    /// Runs <c>-sS</c> and <paramref name="arguments" /> through the production handler set over the
    /// connector the composition builds from them, whose every dial stalls until cancelled.
    /// </summary>
    private async Task<int> RunAsync(params string[] arguments)
    {
        Diagnostics.Arrange("command line", "-sS " + string.Join(" ", arguments));
        Diagnostics.Arrange("dialer", "every dial stalls until cancelled, on a timer that fires at once");
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        TcpConnector connector = CurlComposition.CreateTcpConnector(
            parsed.Options,
            new LoopbackDnsResolver(),
            new StallingTcpDialer(),
            new PassThroughTlsProvider(),
            new ImmediateTimerTimeProvider(),
            HttpProxyTunnelOptions.Default);
        InMemoryFileSystem files = new();

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(
                        new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver())),
                        [],
                        loadResolveEntries: connector.LoadResolveEntries),
                    files,
                    files,
                    new MemoryStream(),
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: true)
                .RunAsync(["-sS", .. arguments]);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stderr, platform newlines as LF", Encoding.ASCII.GetBytes(StandardErrorText.Replace(Environment.NewLine, "\n", StringComparison.Ordinal)));
        return exitCode;
    }

    /// <summary>A dialer whose every dial never completes until its token is cancelled.</summary>
    private sealed class StallingTcpDialer : ITcpDialer
    {
        public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("An infinite delay ended without being cancelled.");
        }

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public async ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("An infinite delay ended without being cancelled.");
        }
    }
}
