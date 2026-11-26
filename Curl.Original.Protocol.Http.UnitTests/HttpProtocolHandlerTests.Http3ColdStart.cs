using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins BL-5660 (ADR-1326): the first QUIC attempt of a process leads TCP by the cold-start
/// allowance on top of the happy-eyeballs timeout, later ones do not, and a QUIC connect that
/// ignores its token still ends at the connect timeout.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private static readonly TimeSpan ColdStartAllowance = TimeSpan.FromSeconds(1);

    [TestMethod]
    public async Task ExecuteAsync_Http3FirstQuicAttemptOfTheProcess_LeadsTcpByTheColdStartAllowance()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();
        TimeSpan headStart = TimeSpan.FromMilliseconds(200) + ColdStartAllowance;

        Diagnostics.Arrange("url, head start ms", $"{RaceUrl}, {headStart.TotalMilliseconds}");
        Task<TransferResult> transfer = ColdStartHandler(connector, new QuicColdStartGate()).ExecuteAsync(RaceContext(time)).AsTask();
        await connector.QuicStarted;
        await time.TimerCreatedAsync(headStart);
        time.Advance(headStart - TimeSpan.FromMilliseconds(1));

        Diagnostics.Assert("tcp started early", false, connector.TcpStarted.IsCompleted);
        Assert.IsFalse(connector.TcpStarted.IsCompleted);

        connector.QuicResult.SetResult(MultiplexedConnectResult.Connected(Http3OkConnection(), null));
        TransferResult result = await transfer;

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("tcp connects", 0, connector.TcpConnects);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, connector.TcpConnects);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3SecondQuicAttemptOfTheProcess_LeadsTcpByTheHappyEyeballsTimeoutAlone()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        QuicColdStartGate gate = new();
        Diagnostics.Arrange("gate", "first cold start already taken");
        Assert.IsTrue(gate.TryTakeColdStart());
        RacingConnector connector = new();

        Task<TransferResult> transfer = ColdStartHandler(connector, gate).ExecuteAsync(RaceContext(time)).AsTask();
        await StartTcpAfterTheTimeoutAsync(connector, time);
        connector.TcpResult.SetResult(ConnectResult.Connected(Connection("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n", 65536)));
        TransferResult result = await transfer;

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("tcp connects", 1, connector.TcpConnects);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(1, connector.TcpConnects);
    }

    [TestMethod]
    public void TryTakeColdStart_CalledTwice_IsTrueOnlyTheFirstTime()
    {
        QuicColdStartGate gate = new();

        Assert.IsTrue(gate.TryTakeColdStart());
        Assert.IsFalse(gate.TryTakeColdStart());
        Assert.IsNotNull(QuicColdStartGate.Process);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3OnlyQuicConnectIgnoresItsToken_EndsAtTheConnectTimeout()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RacingConnector connector = new();

        Diagnostics.Arrange("scripted connects", "quic stalls and ignores its token");
        Task<TransferResult> transfer = RaceHandler(connector).ExecuteAsync(
            Http3Context(RaceUrl, new MemoryStream(), version: HttpVersionPreference.Http3Only, time: time, connectTimeout: RaceConnectTimeout)).AsTask();
        await connector.QuicStarted;
        await time.TimerCreatedAsync(RaceConnectTimeout);
        time.Advance(RaceConnectTimeout);
        TransferResult result = await transfer;

        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
    }

    private static HttpProtocolHandler ColdStartHandler(RacingConnector connector, QuicColdStartGate gate) =>
        new(connector, new SilentAuthenticator()) { QuicColdStartAllowance = ColdStartAllowance, QuicColdStart = gate };
}
