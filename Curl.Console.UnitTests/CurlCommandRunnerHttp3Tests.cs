using System.Net;
using System.Text;
using Curl.Http2;
using Curl.Http3;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>--http3</c> and <c>--http3-only</c> end to end through the production composition
/// over <see cref="ScriptedQuicConnector" /> (BL-732, ADR-0144): a transfer over HTTP/3, the
/// fallback to TCP when the QUIC connect fails, and the failures curl.se's ngtcp2 build reports,
/// on every platform. The QUIC failure text is the one measured on Windows; the connector
/// supplies it, so the test is the same everywhere.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerHttp3Tests
{
    private const string HttpsUrl = "https://localhost:18443/q";

    private const string QuicRecvError = "QUIC: recvfrom() unexpectedly returned -1 (errno=10054; Connection was reset)";

    private static readonly MultiplexedConnectResult QuicRefused = MultiplexedConnectResult.Failed(CurlExitCode.RecvError, QuicRecvError);

    [TestMethod]
    [DataRow("--http3")]
    [DataRow("--http3-only")]
    public async Task RunAsync_Http3OptionAndQuicConnects_RunsTheTransferOverHttp3(string option)
    {
        ScriptedMultiplexedStream stream = new(0, Http3Response("hello"));
        ScriptedMultiplexedConnection quic = new(stream) { RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 18443) };
        ScriptedQuicConnector connector = new(MultiplexedConnectResult.Connected(quic, null), new RecordingConnector(CurlExitCode.CouldntConnect, "unused"));

        (int exitCode, string standardOutput, string standardError) = await RunAsync(connector, "-sS", "-i", "-w", "|%{http_version}", option, HttpsUrl);

        Assert.AreEqual(0, exitCode, standardError);
        Assert.AreEqual("HTTP/3 200 \r\ncontent-length: 5\r\n\r\nhello|3", standardOutput);
        Assert.AreEqual(new ConnectTarget("localhost", 18443, true) { PoolScheme = "https" }, connector.QuicTargets.Single() with { Events = NoTransferEvents.Instance });
        Assert.AreEqual(0, connector.TcpConnectCount);
        Assert.AreEqual(0x100L, quic.CloseCode, "closed with H3_NO_ERROR");
        Assert.IsGreaterThan(0L, stream.Written.Length, "the request went out on the QUIC stream");
    }

    [TestMethod]
    public async Task RunAsync_Http3OnlyVerboseInclude_WritesCurlsLinesAndHttpVersion3()
    {
        // curl -s -v -i --http3-only https://cloudflare-quic.com/ -o out -w '%{http_version}\n' with
        // curl.se's ngtcp2 build (BL-734 Notes): from "using HTTP/3" to "left intact" the lines are
        // the transfer's own; the connect and TLS lines before them come from the QUIC dialer.
        ScriptedMultiplexedStream stream = new(0, Http3Response("hello", ("content-type", "text/html"), ("server", "cloudflare")));
        ScriptedMultiplexedConnection quic = new(stream) { RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 18443) };
        ScriptedQuicConnector connector = new(MultiplexedConnectResult.Connected(quic, null), new RecordingConnector(CurlExitCode.CouldntConnect, "unused"));

        (int exitCode, string standardOutput, string standardError) = await RunAsync(connector, "-s", "-v", "-i", "--http3-only", "-w", "%{http_version}\n", HttpsUrl);

        Assert.AreEqual(0, exitCode, standardError);
        Assert.AreEqual("HTTP/3 200 \r\ncontent-length: 5\r\ncontent-type: text/html\r\nserver: cloudflare\r\n\r\nhello3\n", standardOutput);
        // The header lines keep their CR LF and Windows' text mode adds a CR before each line
        // feed, so every CR is dropped to compare the same text on every platform.
        string verbose = standardError.Replace("\r", string.Empty, StringComparison.Ordinal);
        Assert.AreEqual(
            "* using HTTP/3\n"
            + "* [HTTP/3] [0] OPENED stream for https://localhost:18443/q\n"
            + "* [HTTP/3] [0] [:method: GET]\n"
            + "* [HTTP/3] [0] [:scheme: https]\n"
            + "* [HTTP/3] [0] [:authority: localhost:18443]\n"
            + "* [HTTP/3] [0] [:path: /q]\n"
            + "* [HTTP/3] [0] [user-agent: curl/8.21.0]\n"
            + "* [HTTP/3] [0] [accept: */*]\n"
            + "> GET /q HTTP/3\n"
            + "> Host: localhost:18443\n"
            + "> User-Agent: curl/8.21.0\n"
            + "> Accept: */*\n"
            + "> \n"
            + "* Request completely sent off\n"
            + "< HTTP/3 200 \n"
            + "< content-length: 5\n"
            + "< content-type: text/html\n"
            + "< server: cloudflare\n"
            + "< \n"
            + "{ [5 bytes data]\n"
            + "* Connection #0 to host localhost:18443 left intact\n",
            verbose[verbose.IndexOf("* using HTTP/3", StringComparison.Ordinal)..]);
    }

    [TestMethod]
    public async Task RunAsync_Http3AndQuicFails_FallsBackToTcp()
    {
        ScriptedConnector tcp = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello")]);
        ScriptedQuicConnector connector = new(QuicRefused, tcp);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(connector, "-sS", "--http3", HttpsUrl);

        Assert.AreEqual(0, exitCode, standardError);
        Assert.AreEqual("hello", standardOutput);
        Assert.HasCount(1, connector.QuicTargets, standardError);
        Assert.AreEqual(1, connector.TcpConnectCount);
        StringAssert.StartsWith(Encoding.Latin1.GetString(tcp.Written), "GET /q HTTP/1.1\r\n");
    }

    [TestMethod]
    public async Task RunAsync_Http3AndBothConnectsFail_FailsWithTheQuicAttemptsExitAndMessage()
    {
        ScriptedQuicConnector connector = new(QuicRefused, new RecordingConnector(CurlExitCode.CouldntConnect, "Failed to connect to localhost port 18443 after 0 ms: Could not connect to server"));

        (int exitCode, _, string standardError) = await RunAsync(connector, "-sS", "--http3", HttpsUrl);

        Assert.AreEqual((int)CurlExitCode.RecvError, exitCode);
        Assert.AreEqual($"curl: (56) {QuicRecvError}\n", NormalizedNewLines(standardError));
    }

    [TestMethod]
    public async Task RunAsync_Http3OnlyAndQuicFails_FailsWithoutTryingTcp()
    {
        ScriptedQuicConnector connector = new(QuicRefused, new RecordingConnector(CurlExitCode.CouldntConnect, "unused"));

        (int exitCode, _, string standardError) = await RunAsync(connector, "-sS", "--http3-only", HttpsUrl);

        Assert.AreEqual((int)CurlExitCode.RecvError, exitCode);
        Assert.AreEqual($"curl: (56) {QuicRecvError}\n", NormalizedNewLines(standardError));
        Assert.AreEqual(0, connector.TcpConnectCount);
    }

    [TestMethod]
    public async Task RunAsync_Http3OnlyWithAnHttpUrl_FailsWithExit3BeforeConnecting()
    {
        ScriptedQuicConnector connector = new(QuicRefused, new RecordingConnector(CurlExitCode.CouldntConnect, "unused"));

        (int exitCode, _, string standardError) = await RunAsync(connector, "-sS", "--http3-only", "http://localhost:18080/");

        Assert.AreEqual((int)CurlExitCode.UrlMalformat, exitCode);
        Assert.AreEqual("curl: (3) HTTP/3 requested for non-HTTPS URL\n", NormalizedNewLines(standardError));
        Assert.IsEmpty(connector.QuicTargets);
        Assert.AreEqual(0, connector.TcpConnectCount);
    }

    [TestMethod]
    [DataRow("--http3", "http://localhost:18080/")]
    [DataRow("--http1.1", HttpsUrl)]
    [DataRow("--http2", HttpsUrl)]
    public async Task RunAsync_NoQuicAsked_ConnectsOverTcpOnly(string option, string url)
    {
        ScriptedConnector tcp = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello")]);
        ScriptedQuicConnector connector = new(QuicRefused, tcp);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(connector, "-sS", "--http3", option, url);

        Assert.AreEqual(0, exitCode, standardError);
        Assert.AreEqual("hello", standardOutput);
        Assert.IsEmpty(connector.QuicTargets);
        Assert.AreEqual(1, connector.TcpConnectCount);
    }

    /// <summary>A response on the request stream: a 200 head with the body's length, then the body.</summary>
    [TestMethod]
    public async Task RunAsync_ParallelHttp3OnlyToOneOrigin_CarriesThreeTransfersOnStreams0And4And8OfOneQuicConnection()
    {
        // curl -Z --http3-only -v -w '%{num_connects}\n' with three URLs on one origin, curl.se's
        // ngtcp2 build (BL-735 Notes): one connection, the second and third transfers multiplexed
        // onto it on streams 4 and 8, num_connects 1, 0 and 0, and one "left intact".
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ScriptedMultiplexedStream[] streams = [.. new long[] { 0, 4, 8 }.Select(id => new ScriptedMultiplexedStream(id, Http3Response(string.Empty)) { ReadsAfter = release.Task })];
        ScriptedMultiplexedConnection quic = new(streams) { RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 18443) };
        ScriptedQuicConnector connector = new(MultiplexedConnectResult.Connected(quic, null), new RecordingConnector(CurlExitCode.CouldntConnect, "unused"));

        PoolingConnector pool = new(connector, TimeProvider.System) { WaitsForMultiplexing = true };

        Task<(int ExitCode, string StandardOutput, string StandardError)> run = RunAsync(
            pool, "-Z", "--http3-only", "-s", "-v", "-w", "%{num_connects}\n", HttpsUrl + "1", HttpsUrl + "2", HttpsUrl + "3");
        await Task.WhenAll(streams.Select(stream => stream.FirstWrite)).WaitAsync(TimeSpan.FromSeconds(30));
        release.SetResult();
        (int exitCode, string standardOutput, string standardError) = await run;
        await pool.DisposeAsync();

        Assert.AreEqual(0, exitCode, standardError);
        Assert.HasCount(1, connector.QuicTargets, standardError);
        CollectionAssert.AreEquivalent(new[] { "1", "0", "0" }, NormalizedNewLines(standardOutput).Split('\n', StringSplitOptions.RemoveEmptyEntries));
        string[] lines = NormalizedNewLines(standardError).Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
        CollectionAssert.IsSubsetOf(
            new[] { "* [HTTP/3] [0] OPENED stream for https://localhost:18443/q1", "* [HTTP/3] [4] OPENED stream for https://localhost:18443/q2", "* [HTTP/3] [8] OPENED stream for https://localhost:18443/q3" },
            lines.Where(line => line.Contains("OPENED", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray());
        Assert.HasCount(2, lines.Where(line => line == "* Multiplexed connection found"));
        Assert.HasCount(2, lines.Where(line => line == "* Reusing existing https: connection with host localhost"));
        Assert.HasCount(1, lines.Where(line => line == "* Connection #0 to host localhost:18443 left intact"));
        Assert.AreEqual(1, quic.CloseCount, "closed once, at the end of the run");
        Assert.AreEqual(0x100L, quic.CloseCode);
    }

    private static byte[] Http3Response(string body, params (string Name, string Value)[] headers)
    {
        byte[] head = new QpackEncoder(0, 0).EncodeFieldSection(
            0,
            [
                new HeaderField(":status", "200"),
                new HeaderField("content-length", body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                .. headers.Select(header => new HeaderField(header.Name, header.Value)),
            ]);
        return [.. new Http3HeadersFrame(head).ToBytes(), .. new Http3DataFrame(Encoding.Latin1.GetBytes(body)).ToBytes()];
    }

    private static string NormalizedNewLines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAsync(IConnector connector, params string[] arguments)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        int exitCode = await CurlComposition
            .CreateRunner(standardOutput, standardError, standardInput, connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
            .RunAsync(arguments);

        return (exitCode, Encoding.Latin1.GetString(standardOutput.ToArray()), Encoding.Latin1.GetString(standardError.ToArray()));
    }
}
