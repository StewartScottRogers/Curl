using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins CGR-101 (BL-3630): <c>curl -sS --disable-epsv --no-ftp-skip-pasv-ip --connect-timeout 2
/// ftp://host/file.txt</c> against a <c>227</c> naming the unreachable 10.9.8.7 does not end the
/// passive data dial at 2 s. Measured 2026-10-26 with <c>Record-CurlExchange.ps1 -Ftp</c>: curl
/// 8.21.0 on Linux (OpenSSL) ran until the system gave up after 135 s and on Windows (Schannel)
/// after 21 s, both with <c>curl: (28) Failed to connect to ... via 10.9.8.7:1025 after N ms: Could
/// not connect to server</c>; only curl 8.18.0 ended it at 2 s (ADR-0286).
/// </summary>
[TestClass]
public sealed class Cgr101Tests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteAsync_PassiveDataDialStalledPastTheConnectTimeout_KeepsDialingUntilTheSystemGivesUp()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var clock = new SteppingTimeProvider();
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n227 Entering Passive Mode (10,9,8,7,4,1)\r\n"));
        var dataConnector = new StallingDataConnector();
        var handler = new FtpProtocolHandler(
            new QueuedConnector(ConnectResult.Connected(control)),
            dataConnector,
            new QueuedListener(),
            new QueuedTlsProvider(),
            new NamedDnsResolver(new Dictionary<string, IPAddress[]>()),
            new NamedNetworkInterfaceLookup(new Dictionary<string, IPAddress[]>()));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ftp://127.0.0.1:20986/file.txt"),
            Output = new MemoryStream(),
            ConnectTimeout = TimeSpan.FromSeconds(2),
            TimeProvider = clock,
            FtpDisableEpsv = true,
            FtpSkipPasvIp = false,
        };
        diagnostics.Arrange("url", context.Url);
        diagnostics.Arrange("ConnectTimeout", context.ConnectTimeout);

        Task<TransferResult> run = handler.ExecuteAsync(context).AsTask();
        CancellationToken dialToken = await dataConnector.Dialing;
        clock.Advance(TimeSpan.FromSeconds(130));
        diagnostics.Act("clock advanced past the connect timeout", "130 s");

        diagnostics.Assert("dial cancelled", false, dialToken.IsCancellationRequested);
        Assert.IsFalse(dialToken.IsCancellationRequested);
        Assert.IsFalse(run.IsCompleted);

        dataConnector.GiveUp(ConnectResult.Failed(CurlExitCode.OperationTimedOut, "Failed to connect to 10.9.8.7:1025 after 135022 ms: Could not connect to server"));
        TransferResult result = await run;
        diagnostics.ActResult(result);

        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.OperationTimedOut, "Failed to connect to 127.0.0.1:20986 via 10.9.8.7:1025 after 135022 ms: Could not connect to server"),
            result with { Report = null });
    }

    /// <summary>
    /// A data connector whose dial waits until <see cref="GiveUp" /> answers it, as a dial to an
    /// address that never answers waits for the system's SYN retries; it never ends on its token.
    /// </summary>
    private sealed class StallingDataConnector : IConnector
    {
        private readonly TaskCompletionSource<CancellationToken> dialing = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<ConnectResult> answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets a task that completes with the dial's token once the dial has started.</summary>
        public Task<CancellationToken> Dialing => dialing.Task;

        public void GiveUp(ConnectResult result) => answer.TrySetResult(result);

        public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            dialing.TrySetResult(cancellationToken);
            return await answer.Task;
        }
    }
}
