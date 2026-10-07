using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that the runner disposes the run's connection pool once its transfers end, whatever
/// their outcome, and writes nothing while doing so, as curl's <c>-v</c> shows nothing after
/// the last <c>left intact</c> (ADR-0050).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerConnectionPoolTests
{
    private readonly InMemoryFileSystem fileSystem = new();

    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_SuccessfulTransfer_DisposesTheConnectionPoolOnce()
    {
        RecordingConnectionPool pool = new();

        int exitCode = await RunAsync(
            pool,
            new ScriptedConnector([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 1\r\n\r\na")]),
            "http://h:18234/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("pool dispose count", 1, pool.DisposeCount);
        Diagnostics.Assert("stderr length", 0L, standardError.Length);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(1, pool.DisposeCount);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransfer_DisposesTheConnectionPoolOnce()
    {
        RecordingConnectionPool pool = new();

        int exitCode = await RunAsync(
            pool,
            new RecordingConnector(CurlExitCode.CouldntConnect, "Failed to connect"),
            "http://h:18234/");

        Diagnostics.Assert("exit code", 7, exitCode);
        Diagnostics.Assert("pool dispose count", 1, pool.DisposeCount);
        Diagnostics.Assert("stderr length", 0L, standardError.Length);
        Assert.AreEqual(7, exitCode);
        Assert.AreEqual(1, pool.DisposeCount);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_UrlThatIsNotAWellFormedGlob_StillDisposesTheConnectionPool()
    {
        RecordingConnectionPool pool = new();

        int exitCode = await RunAsync(pool, new RecordingConnector(CurlExitCode.CouldntConnect, "unused"), "http://h/[1-");

        Diagnostics.Assert("exit code", 3, exitCode);
        Diagnostics.Assert("pool dispose count", 1, pool.DisposeCount);
        Assert.AreEqual(3, exitCode);
        Assert.AreEqual(1, pool.DisposeCount);
    }

    [TestMethod]
    public async Task RunAsync_WithARunConnectionCacheAndTwoOptionGroups_ClosesItOnceAfterBoth()
    {
        RecordingConnectionPool runConnections = new();
        int groupsBuilt = 0;
        string[] arguments = ["-s", "http://h:18234/", "--next", "-s", "http://h:18234/"];
        Diagnostics.Arrange("command line", string.Join(" ", arguments));
        Diagnostics.Arrange("connector", "refuses every connect with exit 7");
        Diagnostics.Arrange("run connection cache", "recording pool counting its disposals");

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ =>
                    {
                        groupsBuilt++;
                        return new TransferDispatch(new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(new RecordingConnector(CurlExitCode.CouldntConnect, "Failed to connect"), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver())), []);
                    },
                    fileSystem,
                    fileSystem,
                    new MemoryStream(),
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: false,
                    runConnectionCache: runConnections)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stderr", standardError.ToArray());
        Diagnostics.Assert("exit code", 7, exitCode);
        Diagnostics.Assert("option groups built", 2, groupsBuilt);
        Diagnostics.Assert("run connection cache dispose count", 1, runConnections.DisposeCount);
        Assert.AreEqual(7, exitCode);
        Assert.AreEqual(2, groupsBuilt);
        Assert.AreEqual(1, runConnections.DisposeCount);
    }

    [TestMethod]
    public async Task DisposeAsync_WithoutAConnectionPool_DoesNothing()
    {
        Diagnostics.Arrange("transfer dispatch", "no handlers, no connection pool");
        TransferDispatch dispatch = new(new ProtocolDispatcher([]));

        using (Diagnostics.Phase("dispose"))
        {
            await dispatch.DisposeAsync();
        }

        Diagnostics.Act("connection pool", dispatch.ConnectionPool?.ToString() ?? "<null>");
        Diagnostics.Assert("connection pool", "<null>", dispatch.ConnectionPool?.ToString() ?? "<null>");
        Assert.IsNull(dispatch.ConnectionPool);
    }

    /// <summary>
    /// Runs <paramref name="url" /> silently through the production handler set over
    /// <paramref name="connector" />, with <paramref name="pool" /> as the run's connection pool.
    /// </summary>
    private async Task<int> RunAsync(RecordingConnectionPool pool, IConnector connector, string url)
    {
        Diagnostics.Arrange("command line", "-s " + url);
        Diagnostics.Arrange("connector", connector is ScriptedConnector ? "scripted 200 OK, Content-Length 1, body a" : "refuses every connect with exit 7");

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(
                        new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver())),
                        [],
                        connectionPool: pool),
                    fileSystem,
                    fileSystem,
                    new MemoryStream(),
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: false)
                .RunAsync(["-s", url]);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return exitCode;
    }

    /// <summary>A connection pool that counts how often it is disposed.</summary>
    private sealed class RecordingConnectionPool : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
