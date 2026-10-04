using Curl.Authentication;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// Pins how <c>-Z</c> transfers to one HTTP/2 origin share a connection (BL-717), through the
/// production <see cref="HttpProtocolHandler" /> and <see cref="PoolingConnector" /> over an
/// in-memory HTTP/2 server (<see cref="Http2ServerConnector" />), as curl 8.18.0 (OpenSSL,
/// nghttp2) does with <c>--http2-prior-knowledge</c> (measured, BL-717 Notes): three URLs go out
/// on streams 1, 3 and 5 of one connection, <c>%{num_connects}</c> is 1 for the transfer that
/// opened it and 0 for the others, a transfer that finds the server's
/// <c>SETTINGS_MAX_CONCURRENT_STREAMS</c> all taken opens a connection of its own, and under
/// <c>--parallel-immediate</c> transfers that find the first connection still being opened open
/// their own.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerHttp2MultiplexingTests
{
    private const string WriteOut = "%{url} %{num_connects}\\n";

    private readonly InMemoryFileSystem fileSystem = new();

    private readonly TextWaitingStream standardOutput = new();

    private readonly TextWaitingStream standardError = new();

    [TestMethod]
    public async Task RunAsync_ThreeUrlsToOneOrigin_CarriesThemOnStreamsOneThreeAndFiveOfOneConnection()
    {
        Http2ServerConnector server = new() { HoldsResponses = true };

        Task<int> run = RunAsync(server, ["-Z", "--http2-prior-knowledge", "-s", "-w", WriteOut, "http://h/1", "http://h/2", "http://h/3"]);
        await server.To("h").Single().StreamsOpenedAsync(3);
        server.Opened[0].ReleaseResponses();

        Assert.AreEqual(0, await run);
        Http2ServerConnection connection = server.To("h").Single();
        CollectionAssert.AreEqual(new[] { 1, 3, 5 }, connection.StreamIds);
        CollectionAssert.AreEquivalent(new[] { "http://h/1 1", "http://h/2 0", "http://h/3 0" }, Lines(standardOutput.Text));
    }

    [TestMethod]
    public async Task RunAsync_ThreeUrlsWithVerbose_ReportsTheSharedConnectionAndLeavesItIntactOnce()
    {
        Http2ServerConnector server = new() { HoldsResponses = true };

        Task<int> run = RunAsync(server, ["-Z", "--http2-prior-knowledge", "-v", "-s", "http://h/1", "http://h/2", "http://h/3"]);
        await server.To("h").Single().StreamsOpenedAsync(3);
        server.Opened[0].ReleaseResponses();

        Assert.AreEqual(0, await run);
        string[] lines = Lines(standardError.Text);
        Assert.HasCount(2, lines.Where(line => line == "* Multiplexed connection found"));
        Assert.HasCount(2, lines.Where(line => line == "* Reusing existing http: connection with host h"));
        Assert.HasCount(1, lines.Where(line => line == "* Connection #0 to host h:80 left intact"));
    }

    [TestMethod]
    public async Task RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer()
    {
        Http2ServerConnector server = new(maxConcurrentStreams: 1) { HoldsResponses = true };

        Task<int> run = RunAsync(server, ["-Z", "--http2-prior-knowledge", "--parallel-max", "2", "-v", "-s", "-w", WriteOut, "http://h/1", "http://other/2", "http://h/3"]);
        Http2ServerConnection first = server.To("h").Single();
        await first.StreamsOpenedAsync(1);
        await first.ReadsAskedAsync(2);
        Http2ServerConnection other = server.To("other").Single();
        await other.StreamsOpenedAsync(1);
        other.ReleaseResponses();
        await standardOutput.WhenEndsWithAsync("http://other/2 1\n");
        await WhenOpenedAsync(server, "h", 2);
        Http2ServerConnection second = server.To("h")[1];
        await second.StreamsOpenedAsync(1);
        first.ReleaseResponses();
        second.ReleaseResponses();

        Assert.AreEqual(0, await run);
        CollectionAssert.AreEqual(new[] { 1 }, first.StreamIds);
        CollectionAssert.AreEqual(new[] { 1 }, second.StreamIds);
        CollectionAssert.Contains(Lines(standardError.Text), "* MAX_CONCURRENT_STREAMS reached, skip (1)");
        CollectionAssert.AreEquivalent(new[] { "http://h/1 1", "http://other/2 1", "http://h/3 1" }, Lines(standardOutput.Text));
    }

    [TestMethod]
    public async Task RunAsync_ParallelImmediateWhileTheFirstConnectionIsBeingOpened_OpensOneConnectionPerTransfer()
    {
        Http2ServerConnector server = new(connectsHeldTogether: 3);

        int exitCode = await RunAsync(server, ["-Z", "--parallel-immediate", "--http2-prior-knowledge", "-s", "-w", WriteOut, "http://h/1", "http://h/2", "http://h/3"]);

        Assert.AreEqual(0, exitCode);
        Assert.HasCount(3, server.To("h"));
        Assert.IsTrue(server.Opened.All(connection => connection.StreamIds.SequenceEqual([1])));
        CollectionAssert.AreEquivalent(new[] { "http://h/1 1", "http://h/2 1", "http://h/3 1" }, Lines(standardOutput.Text));
    }

    private static string[] Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToArray();

    private static async Task WhenOpenedAsync(Http2ServerConnector server, string host, int count)
    {
        while (server.To(host).Length < count)
        {
            await Task.Yield();
        }
    }

    /// <summary>
    /// Runs <paramref name="arguments" /> through the HTTP handler over a pool of
    /// <paramref name="server" />'s connections, the pool waiting for multiplexing as the
    /// composition sets it (<see cref="CurlComposition.WaitsForMultiplexing" />).
    /// </summary>
    private Task<int> RunAsync(Http2ServerConnector server, string[] arguments) =>
        new CurlCommandRunner(
                options =>
                {
                    PoolingConnector pool = new(server, TimeProvider.System) { WaitsForMultiplexing = CurlComposition.WaitsForMultiplexing(options) };
                    HttpProtocolHandler http = new(pool, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
                    return new TransferDispatch(new ProtocolDispatcher([http]), [], connectionPool: pool);
                },
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                parsesAsWindowsBuild: false,
                writesProgressMeter: false)
            .RunAsync(arguments);
}
