using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins third-party gap report issue CGR-231: a download whose <c>226</c> reply never arrives
/// ends with exit 28 and curl 8.22.0's <c>FTP response timeout</c>, whether the overall time
/// limit or the server response timeout runs out first.
/// </summary>
[TestClass]
public sealed class Cgr231Tests
{
    private const string Replies = "220 Ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"
        + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 10\r\n150 Opening data connection\r\n";

    [TestMethod]
    public async Task ExecuteAsync_ServerResponseTimeoutPassesBefore226_SaysFtpResponseTimeout()
    {
        TransferResult result = await RunAsync(new CancellationTokenSource(), cancelOnStall: false);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("FTP response timeout", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_OverallTimeLimitPassesBefore226_SaysFtpResponseTimeout()
    {
        TransferResult result = await RunAsync(new CancellationTokenSource(), cancelOnStall: true);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("FTP response timeout", result.ErrorMessage);
    }

    private static async Task<TransferResult> RunAsync(CancellationTokenSource limit, bool cancelOnStall)
    {
        var control = new StallingConnection(Encoding.Latin1.GetBytes(Replies));
        var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes("0123456789"))));
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse("ftp://127.0.0.1:18321/f.txt"), Output = new MemoryStream() },
            mutable =>
            {
                mutable.CancellationToken = limit.Token;
                mutable.FtpServerResponseTimeout = cancelOnStall ? TimeSpan.FromMinutes(5) : TimeSpan.FromMilliseconds(50);
            });
        Task<TransferResult> run = new FtpProtocolHandler(connector).ExecuteAsync(context).AsTask();
        if (cancelOnStall)
        {
            await control.Stalled;
            await limit.CancelAsync();
        }

        return await run;
    }
}
