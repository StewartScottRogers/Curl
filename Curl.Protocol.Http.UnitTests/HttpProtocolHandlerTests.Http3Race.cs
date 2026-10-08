using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins the <c>--http3</c> race of QUIC against TCP (BL-835, ADR-0144 section 4) on a fake
/// clock: the TCP connect starts when the QUIC connect fails or once the happy-eyeballs
/// timeout passes without it completing, the first to connect carries the transfer, and the
/// other is cancelled and its connection disposed should it still complete.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string RaceUrl = "https://127.0.0.1:18731/";

    private static readonly TimeSpan RaceConnectTimeout = TimeSpan.FromSeconds(60);

    [TestMethod]
    [DataRow(0, DisplayName = "default 200 ms")]
    [DataRow(1000, DisplayName = "--happy-eyeballs-timeout-ms 1000")]
    public async Task ExecuteAsync_Http3QuicHandshakePending_StartsTcpOnceTheHappyEyeballsTimeoutPasses(int timeoutMilliseconds)
    {
        TimeSpan timeout = timeoutMilliseconds == 0 ? TimeSpan.FromMilliseconds(200) : TimeSpan.FromMilliseconds(timeoutMilliseconds);
        HttpRequestOptions options = timeoutMilliseconds == 0 ? new HttpRequestOptions() : new HttpRequestOptions { HappyEyeballsTimeout = timeout };
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();

        Diagnostics.Arrange("url, happy eyeballs timeout ms", $"{RaceUrl}, {timeout.TotalMilliseconds}");
        Diagnostics.Arrange("scripted connects", "quic pending, tcp 200 ok");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time, options: options)).AsTask();
        await connector.QuicStarted;
        await time.TimerCreatedAsync(timeout);
        time.Advance(timeout - TimeSpan.FromMilliseconds(1));

        Diagnostics.Act("tcp started one ms before timeout", connector.TcpStarted.IsCompleted);
        Diagnostics.Assert("tcp started early", false, connector.TcpStarted.IsCompleted);
        Assert.IsFalse(connector.TcpStarted.IsCompleted);

        time.Advance(TimeSpan.FromMilliseconds(1));
        await connector.TcpStarted;
        connector.TcpResult.SetResult(ConnectResult.Connected(Connection("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n", 65536)));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("tcp connects", 1, connector.TcpConnects);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(1, connector.TcpConnects);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3QuicConnectsBeforeTheTimeout_NeverStartsTcp()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();

        Diagnostics.Arrange("url", RaceUrl);
        Diagnostics.Arrange("scripted connects", "quic connects before timeout, h3 200");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time)).AsTask();
        await connector.QuicStarted;
        connector.QuicResult.SetResult(MultiplexedConnectResult.Connected(Http3OkConnection(), null));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("http version", new Version(3, 0), result.Report!.HttpVersion);
        Diagnostics.Assert("tcp connects", 0, connector.TcpConnects);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(new Version(3, 0), result.Report!.HttpVersion);
        Assert.AreEqual(0, connector.TcpConnects);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3QuicFailsBeforeTheTimeout_StartsTcpAtOnce()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();
        MemoryStream output = new();

        Diagnostics.Arrange("url", RaceUrl);
        Diagnostics.Arrange("scripted connects", "quic fails RecvError, tcp 200 ok");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time, output)).AsTask();
        await connector.QuicStarted;
        connector.QuicResult.SetResult(MultiplexedConnectResult.Failed(CurlExitCode.RecvError, "QUIC: recvfrom() unexpectedly returned -1"));
        await connector.TcpStarted;
        connector.TcpResult.SetResult(ConnectResult.Connected(Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536)));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("clock", DateTimeOffset.UnixEpoch, time.GetUtcNow());
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("output", "ok", Latin1(output.ToArray()));
        Assert.AreEqual(DateTimeOffset.UnixEpoch, time.GetUtcNow());
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
    }

    [TestMethod]
    [DataRow("connects", DisplayName = "the QUIC connect completes later")]
    [DataRow("fails", DisplayName = "the QUIC connect fails later")]
    [DataRow("throws", DisplayName = "the QUIC connect throws later")]
    public async Task ExecuteAsync_Http3TcpWins_CarriesTheTransferOverTcpAndAbandonsTheQuicConnect(string quicLater)
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();
        MemoryStream output = new();

        Diagnostics.Arrange("url, late quic outcome", $"{RaceUrl}, {quicLater}");
        Diagnostics.Arrange("scripted connects", "quic pending, tcp 200 tcp after timeout");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time, output)).AsTask();
        await StartTcpAfterTheTimeoutAsync(connector, time);
        connector.TcpResult.SetResult(ConnectResult.Connected(Connection("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\ntcp", 65536)));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("http version", new Version(1, 1), result.Report!.HttpVersion);
        Diagnostics.Diff("output", "tcp", Latin1(output.ToArray()));
        Diagnostics.Assert("quic cancelled", true, connector.QuicToken.IsCancellationRequested);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(new Version(1, 1), result.Report!.HttpVersion);
        Assert.AreEqual("tcp", Latin1(output.ToArray()));
        Assert.IsTrue(connector.QuicToken.IsCancellationRequested);

        FakeMultiplexedConnection lateQuic = new();
        CompleteLater(connector.QuicResult, quicLater, MultiplexedConnectResult.Connected(lateQuic, null), MultiplexedConnectResult.Failed(CurlExitCode.OperationTimedOut, "abandoned"));
        if (quicLater == "connects")
        {
            await lateQuic.Disposed;
        }
    }

    [TestMethod]
    [DataRow("connects", DisplayName = "the TCP connect completes later")]
    [DataRow("fails", DisplayName = "the TCP connect fails later")]
    [DataRow("throws", DisplayName = "the TCP connect throws later")]
    public async Task ExecuteAsync_Http3QuicWinsAfterTheTimeout_CarriesTheTransferOverHttp3AndAbandonsTheTcpConnect(string tcpLater)
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();

        Diagnostics.Arrange("url, late tcp outcome", $"{RaceUrl}, {tcpLater}");
        Diagnostics.Arrange("scripted connects", "tcp pending after timeout, quic connects, h3 200");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time)).AsTask();
        await StartTcpAfterTheTimeoutAsync(connector, time);
        connector.QuicResult.SetResult(MultiplexedConnectResult.Connected(Http3OkConnection(), null));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("http version", new Version(3, 0), result.Report!.HttpVersion);
        Diagnostics.Assert("tcp cancelled", true, connector.TcpToken.IsCancellationRequested);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(new Version(3, 0), result.Report!.HttpVersion);
        Assert.IsTrue(connector.TcpToken.IsCancellationRequested);

        ScriptedConnection lateTcp = Connection(string.Empty, 65536);
        CompleteLater(connector.TcpResult, tcpLater, ConnectResult.Connected(lateTcp), ConnectResult.Failed(CurlExitCode.OperationTimedOut, "abandoned"));
        if (tcpLater == "connects")
        {
            await lateTcp.Disposed;
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3TcpFailsThenQuicConnects_CarriesTheTransferOverHttp3()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();

        connector.TcpResult.SetResult(ConnectResult.Refused("Failed to connect to 127.0.0.1 port 18731 after 0 ms: Could not connect to server"));

        Diagnostics.Arrange("url", RaceUrl);
        Diagnostics.Arrange("scripted connects", "tcp refused at once, quic connects after timeout, h3 200");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time)).AsTask();
        await StartTcpAfterTheTimeoutAsync(connector, time);
        connector.QuicResult.SetResult(MultiplexedConnectResult.Connected(Http3OkConnection(), null));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("http version", new Version(3, 0), result.Report!.HttpVersion);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(new Version(3, 0), result.Report!.HttpVersion);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "TCP fails first")]
    [DataRow(false, DisplayName = "QUIC fails first")]
    public async Task ExecuteAsync_Http3BothFailAfterTheTimeout_FailsWithTheQuicAttemptsExitAndMessage(bool tcpFailsFirst)
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();
        ConnectResult tcpFailure = ConnectResult.Refused("Failed to connect to 127.0.0.1 port 18731 after 200 ms: Could not connect to server", null, connectionNumber: 4);
        MultiplexedConnectResult quicFailure = MultiplexedConnectResult.Failed(CurlExitCode.RecvError, "QUIC: recvfrom() unexpectedly returned -1");
        RecordingTransferEvents events = new();

        if (tcpFailsFirst)
        {
            connector.TcpResult.SetResult(tcpFailure);
        }

        Diagnostics.Arrange("url, tcp fails first", $"{RaceUrl}, {tcpFailsFirst}");
        Diagnostics.Arrange("scripted connects", "quic fails RecvError, tcp refused connection #4");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time, events: events)).AsTask();
        await StartTcpAfterTheTimeoutAsync(connector, time);
        connector.QuicResult.SetResult(quicFailure);
        connector.TcpResult.TrySetResult(tcpFailure);
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        Diagnostics.Assert("error message", "QUIC: recvfrom() unexpectedly returned -1", result.ErrorMessage);
        Diagnostics.Assert("info has closing connection #4", true, events.Info.Contains("closing connection #4"));
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("QUIC: recvfrom() unexpectedly returned -1", result.ErrorMessage);
        CollectionAssert.Contains(events.Info, "closing connection #4");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3OnlyHandshakePending_NeverStartsTcp()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();

        Diagnostics.Arrange("url, version", $"{RaceUrl}, http3-only");
        Diagnostics.Arrange("scripted connects", "quic pending 5 s then SendError");
        Task<TransferResult> transfer = RaceHandler(connector)
            .ExecuteAsync(Http3Context(RaceUrl, new MemoryStream(), time: time, connectTimeout: RaceConnectTimeout)).AsTask();
        await connector.QuicStarted;
        for (int step = 0; step < 50; step++)
        {
            time.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Yield();
        }

        connector.QuicResult.SetResult(MultiplexedConnectResult.Failed(CurlExitCode.SendError, "ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT"));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.SendError, result.ExitCode);
        Diagnostics.Assert("tcp connects", 0, connector.TcpConnects);
        Diagnostics.Assert("tcp started", false, connector.TcpStarted.IsCompleted);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual(0, connector.TcpConnects);
        Assert.IsFalse(connector.TcpStarted.IsCompleted);
    }

    private static HttpProtocolHandler RaceHandler(RacingConnector connector) => new(connector, new SilentAuthenticator());

    private static TransferContext RaceContext(
        FakeTimeProvider time,
        Stream? output = null,
        HttpRequestOptions? options = null,
        RecordingTransferEvents? events = null) =>
        Http3Context(RaceUrl, output ?? new MemoryStream(), options: options, events: events, version: HttpVersionPreference.Http3, time: time, connectTimeout: RaceConnectTimeout);

    /// <summary>
    /// Waits for the QUIC connect, passes the default 200 ms happy-eyeballs timeout on the
    /// clock and waits for the TCP connect it starts.
    /// </summary>
    private static async Task StartTcpAfterTheTimeoutAsync(RacingConnector connector, FakeTimeProvider time)
    {
        await connector.QuicStarted;
        await time.TimerCreatedAsync(TimeSpan.FromMilliseconds(200));
        time.Advance(TimeSpan.FromMilliseconds(200));
        await connector.TcpStarted;
    }

    /// <summary>
    /// Completes an abandoned connect: with <paramref name="connected" />, with
    /// <paramref name="failed" />, or by throwing.
    /// </summary>
    private static void CompleteLater<TResult>(TaskCompletionSource<TResult> source, string outcome, TResult connected, TResult failed)
    {
        switch (outcome)
        {
            case "connects":
                source.SetResult(connected);
                break;
            case "fails":
                source.SetResult(failed);
                break;
            default:
                source.SetException(new InvalidOperationException("The abandoned connect broke."));
                break;
        }
    }

    private static FakeMultiplexedConnection Http3OkConnection() =>
        new(new FakeMultiplexedStream(0, Http3Response(Http3Head("200", ("content-length", "0")))));
}
