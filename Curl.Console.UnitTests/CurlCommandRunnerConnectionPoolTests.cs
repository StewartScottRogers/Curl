using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public async Task RunAsync_SuccessfulTransfer_DisposesTheConnectionPoolOnce()
    {
        RecordingConnectionPool pool = new();

        int exitCode = await RunAsync(
            pool,
            new ScriptedConnector([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 1\r\n\r\na")]),
            "http://h:18234/");

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

        Assert.AreEqual(7, exitCode);
        Assert.AreEqual(1, pool.DisposeCount);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_UrlThatIsNotAWellFormedGlob_StillDisposesTheConnectionPool()
    {
        RecordingConnectionPool pool = new();

        int exitCode = await RunAsync(pool, new RecordingConnector(CurlExitCode.CouldntConnect, "unused"), "http://h/[1-");

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual(1, pool.DisposeCount);
    }

    [TestMethod]
    public async Task DisposeAsync_WithoutAConnectionPool_DoesNothing()
    {
        TransferDispatch dispatch = new(new ProtocolDispatcher([]));

        await dispatch.DisposeAsync();

        Assert.IsNull(dispatch.ConnectionPool);
    }

    /// <summary>
    /// Runs <paramref name="url" /> silently through the production handler set over
    /// <paramref name="connector" />, with <paramref name="pool" /> as the run's connection pool.
    /// </summary>
    private Task<int> RunAsync(RecordingConnectionPool pool, IConnector connector, string url) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(
                    new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))),
                    [],
                    connectionPool: pool),
                fileSystem,
                fileSystem,
                new MemoryStream(),
                standardError,
                new MemoryStream(),
                runsOnWindows: false)
            .RunAsync(["-s", url]);

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
