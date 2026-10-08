using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Pins the <c>--http3</c> race with TCP as the preferred first attempt
/// (<see cref="HttpRequestOptions.TriesTcpBeforeQuic" />), as curl 8.21.0 runs it for an
/// <c>--alt-svc</c> entry naming the origin itself with <c>h2</c> (<c>cf_hc_get_pref_alpn</c>, BL-948):
/// the QUIC connect starts when the TCP connect fails or once the happy-eyeballs timeout passes,
/// the first to connect carries the transfer, and when both fail the TCP attempt's failure is the
/// transfer's.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private static readonly HttpRequestOptions TcpFirst = new() { TcpFirstAttemptVersion = "h2" };

    [TestMethod]
    public async Task ExecuteAsync_Http3TcpFirstAndTcpPending_StartsQuicOnlyOnceTheHappyEyeballsTimeoutPasses()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();

        Diagnostics.Arrange("url, tcp first attempt version", $"{RaceUrl}, h2");
        Diagnostics.Arrange("scripted connects", "tcp pending, quic connects after 200 ms, h3 200");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time, options: TcpFirst)).AsTask();
        await connector.TcpStarted;
        await time.TimerCreatedAsync(TimeSpan.FromMilliseconds(200));
        time.Advance(TimeSpan.FromMilliseconds(199));

        Diagnostics.Act("quic started 199 ms in", connector.QuicStarted.IsCompleted);
        Diagnostics.Assert("quic started early", false, connector.QuicStarted.IsCompleted);
        Assert.IsFalse(connector.QuicStarted.IsCompleted);

        time.Advance(TimeSpan.FromMilliseconds(1));
        await connector.QuicStarted;
        connector.QuicResult.SetResult(MultiplexedConnectResult.Connected(Http3OkConnection(), null));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("http version", new Version(3, 0), result.Report!.HttpVersion);
        Diagnostics.Assert("tcp cancelled", true, connector.TcpToken.IsCancellationRequested);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(new Version(3, 0), result.Report!.HttpVersion);
        Assert.IsTrue(connector.TcpToken.IsCancellationRequested);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3TcpFirstAndTcpConnectsBeforeTheTimeout_NeverStartsQuic()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();
        MemoryStream output = new();

        Diagnostics.Arrange("url, tcp first attempt version", $"{RaceUrl}, h2");
        Diagnostics.Arrange("scripted connects", "tcp connects before timeout, 200 tcp");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time, output, TcpFirst)).AsTask();
        await connector.TcpStarted;
        connector.TcpResult.SetResult(ConnectResult.Connected(Connection("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\ntcp", 65536)));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("output", "tcp", Latin1(output.ToArray()));
        Diagnostics.Assert("quic started", false, connector.QuicStarted.IsCompleted);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("tcp", Latin1(output.ToArray()));
        Assert.IsFalse(connector.QuicStarted.IsCompleted);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3TcpFirstAndTcpFailsBeforeTheTimeout_StartsQuicAtOnce()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();
        connector.TcpResult.SetResult(ConnectResult.Refused("Failed to connect to 127.0.0.1 port 18731 after 0 ms: Could not connect to server"));

        Diagnostics.Arrange("url, tcp first attempt version", $"{RaceUrl}, h2");
        Diagnostics.Arrange("scripted connects", "tcp refused at once, quic connects, h3 200");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time, options: TcpFirst)).AsTask();
        await connector.QuicStarted;
        connector.QuicResult.SetResult(MultiplexedConnectResult.Connected(Http3OkConnection(), null));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("clock", DateTimeOffset.UnixEpoch, time.GetUtcNow());
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("http version", new Version(3, 0), result.Report!.HttpVersion);
        Assert.AreEqual(DateTimeOffset.UnixEpoch, time.GetUtcNow());
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(new Version(3, 0), result.Report!.HttpVersion);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3TcpFirst_GivesBothAttemptsOneTargetNamingTheTcpFirstAttemptVersion()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();
        connector.TcpResult.SetResult(ConnectResult.Refused("Failed to connect to 127.0.0.1 port 18731 after 0 ms: Could not connect to server"));

        Diagnostics.Arrange("url, tcp first attempt version", $"{RaceUrl}, h1");
        Diagnostics.Arrange("scripted connects", "tcp refused at once, quic connects, h3 200");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time, options: TcpFirst with { TcpFirstAttemptVersion = "h1" })).AsTask();
        await connector.QuicStarted;
        connector.QuicResult.SetResult(MultiplexedConnectResult.Connected(Http3OkConnection(), null));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("tcp target first attempt version", "h1", connector.TcpTarget!.TcpFirstAttemptVersion);
        Diagnostics.Assert("quic target is tcp target", true, ReferenceEquals(connector.TcpTarget, connector.QuicTarget));
        Assert.AreEqual("h1", connector.TcpTarget!.TcpFirstAttemptVersion);
        Assert.AreSame(connector.TcpTarget, connector.QuicTarget);
    }

    [TestMethod]
    [DataRow("connects", DisplayName = "the QUIC connect completes later")]
    [DataRow("fails", DisplayName = "the QUIC connect fails later")]
    [DataRow("throws", DisplayName = "the QUIC connect throws later")]
    public async Task ExecuteAsync_Http3TcpFirstAndTcpWinsAfterTheTimeout_CarriesTheTransferOverTcpAndAbandonsQuic(string quicLater)
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();
        MemoryStream output = new();

        Diagnostics.Arrange("url, late quic outcome", $"{RaceUrl}, {quicLater}");
        Diagnostics.Arrange("scripted connects", "tcp wins after timeout, 200 tcp, quic pending");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time, output, TcpFirst)).AsTask();
        await StartQuicAfterTheTimeoutAsync(connector, time);
        connector.TcpResult.SetResult(ConnectResult.Connected(Connection("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\ntcp", 65536)));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("output", "tcp", Latin1(output.ToArray()));
        Diagnostics.Assert("quic cancelled", true, connector.QuicToken.IsCancellationRequested);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
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
    public async Task ExecuteAsync_Http3TcpFirstAndQuicWinsAfterTheTimeout_CarriesTheTransferOverHttp3AndAbandonsTcp(string tcpLater)
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();

        Diagnostics.Arrange("url, late tcp outcome", $"{RaceUrl}, {tcpLater}");
        Diagnostics.Arrange("scripted connects", "tcp pending after timeout, quic connects, h3 200");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time, options: TcpFirst)).AsTask();
        await StartQuicAfterTheTimeoutAsync(connector, time);
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
    public async Task ExecuteAsync_Http3TcpFirstAndQuicFailsThenTcpConnects_CarriesTheTransferOverTcp()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();
        MemoryStream output = new();

        Diagnostics.Arrange("url, tcp first attempt version", $"{RaceUrl}, h2");
        Diagnostics.Arrange("scripted connects", "quic fails RecvError after timeout, then tcp 200 tcp");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time, output, TcpFirst)).AsTask();
        await StartQuicAfterTheTimeoutAsync(connector, time);
        connector.QuicResult.SetResult(MultiplexedConnectResult.Failed(CurlExitCode.RecvError, "QUIC: recvfrom() unexpectedly returned -1"));
        await Task.Yield();
        connector.TcpResult.SetResult(ConnectResult.Connected(Connection("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\ntcp", 65536)));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("output", "tcp", Latin1(output.ToArray()));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("tcp", Latin1(output.ToArray()));
    }

    [TestMethod]
    [DataRow(true, DisplayName = "TCP fails first")]
    [DataRow(false, DisplayName = "QUIC fails first")]
    public async Task ExecuteAsync_Http3TcpFirstAndBothFail_FailsWithTheTcpAttemptsExitAndMessage(bool tcpFailsFirst)
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();
        ConnectResult tcpFailure = ConnectResult.Refused("Failed to connect to 127.0.0.1 port 18731 after 200 ms: Could not connect to server");
        MultiplexedConnectResult quicFailure = MultiplexedConnectResult.Failed(CurlExitCode.RecvError, "QUIC: recvfrom() unexpectedly returned -1");

        Diagnostics.Arrange("url, tcp fails first", $"{RaceUrl}, {tcpFailsFirst}");
        Diagnostics.Arrange("scripted connects", "tcp refused after 200 ms, quic fails RecvError");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(RaceContext(time, options: TcpFirst)).AsTask();
        await StartQuicAfterTheTimeoutAsync(connector, time);
        if (tcpFailsFirst)
        {
            connector.TcpResult.SetResult(tcpFailure);
            await Task.Yield();
        }

        connector.QuicResult.SetResult(quicFailure);
        connector.TcpResult.TrySetResult(tcpFailure);
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("error message", "Failed to connect to 127.0.0.1 port 18731 after 200 ms: Could not connect to server", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1 port 18731 after 200 ms: Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3OnlyWithTcpFirst_IgnoresItAndConnectsOverQuicAlone()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();

        Diagnostics.Arrange("url, version, tcp first attempt version", $"{RaceUrl}, http3-only, h2");
        Diagnostics.Arrange("scripted connects", "quic connects, h3 200");
        Task<TransferResult> transfer = RaceHandler(connector)
            .ExecuteAsync(Http3Context(RaceUrl, new MemoryStream(), options: TcpFirst, time: time, connectTimeout: RaceConnectTimeout)).AsTask();
        await connector.QuicStarted;
        connector.QuicResult.SetResult(MultiplexedConnectResult.Connected(Http3OkConnection(), null));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("tcp connects", 0, connector.TcpConnects);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, connector.TcpConnects);
    }

    /// <summary>
    /// Waits for the TCP connect, passes the default 200 ms happy-eyeballs timeout on the clock and
    /// waits for the QUIC connect it starts.
    /// </summary>
    private static async Task StartQuicAfterTheTimeoutAsync(RacingConnector connector, FakeTimeProvider time)
    {
        await connector.TcpStarted;
        await time.TimerCreatedAsync(TimeSpan.FromMilliseconds(200));
        time.Advance(TimeSpan.FromMilliseconds(200));
        await connector.QuicStarted;
    }
}
