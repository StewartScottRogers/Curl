using System.Text;
using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins BL-6064: what the session says when <c>-m</c> passes while it waits on the server, as curl 8.22.0 was
/// measured to on 2026-11-26 with <c>Record-CurlExchange.ps1 -Ftp</c>.
/// </summary>
[TestClass]
public sealed class FtpMaxTimeReplyWaitTests
{
    private const string ClosingLineDataKey = "Curl.Engine.ClosingConnectionLine";

    private const string UpToType = "220 Ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"
        + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n";

    private const string UpToRetr = UpToType + "213 10\r\n150 Opening data connection\r\n";

    [TestMethod]
    public async Task ExecuteAsync_TimeLimitPassesWhileTheSizeReplyIsAwaited_NamesTheClosingConnectionLine()
    {
        var control = new StallingConnection(Encoding.Latin1.GetBytes(UpToType));

        OperationCanceledException cancelled = await CancelOnStallAsync(control, new ScriptedConnection(), control);

        Assert.AreEqual("closing connection #0", cancelled.Data[ClosingLineDataKey]);
    }

    [TestMethod]
    public async Task ExecuteAsync_TimeLimitPassesPartWayThroughADownload_NamesTheClosingConnectionLine()
    {
        var data = new StallingConnection(Encoding.Latin1.GetBytes("012345"));

        OperationCanceledException cancelled = await CancelOnStallAsync(new StallingConnection(Encoding.Latin1.GetBytes(UpToRetr)), data, data);

        Assert.AreEqual("closing connection #0", cancelled.Data[ClosingLineDataKey]);
    }

    [TestMethod]
    public async Task ExecuteAsync_TimeLimitPassesWhileTheEndOfTransferReplyIsAwaited_WritesThreeLinesAndNoResponseCode()
    {
        var control = new StallingConnection(Encoding.Latin1.GetBytes(UpToRetr));
        var events = new RecordingTransferEvents();
        using var limit = new CancellationTokenSource();
        Task<TransferResult> run = StartAsync(control, new ScriptedConnection(Encoding.Latin1.GetBytes("0123456789")), events, limit.Token);

        await control.Stalled;
        await limit.CancelAsync();
        TransferResult result = await run;

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        string[] lastLines = [.. events.Info.TakeLast(3)];
        string[] expected = ["FTP response timeout", "control connection looks dead", "shutting down connection #0"];
        CollectionAssert.AreEqual(expected, lastLines);
        Assert.AreEqual(0, result.Report!.ResponseCode);
        Assert.AreEqual(0, result.Report.RequestSize);
        Assert.IsFalse(Encoding.Latin1.GetString(control.Sent).Contains("QUIT", StringComparison.Ordinal));
    }

    private static async Task<OperationCanceledException> CancelOnStallAsync(IConnection control, IConnection data, StallingConnection stalling)
    {
        using var limit = new CancellationTokenSource();
        Task<TransferResult> run = StartAsync(control, data, new RecordingTransferEvents(), limit.Token);

        await stalling.Stalled;
        await limit.CancelAsync();

        return await Assert.ThrowsAsync<OperationCanceledException>(() => run);
    }

    private static Task<TransferResult> StartAsync(IConnection control, IConnection data, ITransferEvents events, CancellationToken cancellationToken)
    {
        var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Connected(data));
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse("ftp://127.0.0.1:18321/f.txt"), Output = new MemoryStream() },
            mutable =>
            {
                mutable.CancellationToken = cancellationToken;
                mutable.Events = events;
            });
        return new FtpProtocolHandler(connector).ExecuteAsync(context).AsTask();
    }
}
